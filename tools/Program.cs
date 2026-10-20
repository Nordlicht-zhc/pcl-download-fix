using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length < 2) throw new ArgumentException("inspect ASSEMBLY or merge ORIGINAL DONOR OUTPUT");
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetFullPath("source/PCLCS/bin"));
resolver.AddSearchDirectory(Path.GetFullPath("source/Plain Craft Launcher 2/Resources"));
resolver.AddSearchDirectory(@"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8");
using var original = ModuleDefinition.ReadModule(args[1], new ReaderParameters { AssemblyResolver = resolver });
IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types) => types.SelectMany(t => new[] { t }.Concat(AllTypes(t.NestedTypes)));
if (args[0] == "inspect")
{
    foreach (var t in AllTypes(original.Types).Where(t => t.FullName.StartsWith("PCL.PageDownloadInstall") || t.FullName.StartsWith("PCL.ModModpack")))
    foreach (var m in t.Methods.Where(m => m.Name is "LoaderInit" or "ModpackInstall" || (t.FullName.Contains("Closure") && (m.Name.Contains("0-") || m.Name.Contains("R3-") || m.Name.Contains("3-")))))
    {
        Console.WriteLine(m.FullName);
        if (m.HasBody) foreach(var i in m.Body.Instructions) Console.WriteLine("  " + i);
    }
    return;
}
if (args[0] == "verify")
{
    using var rebuilt = ModuleDefinition.ReadModule(args[2], new ReaderParameters { AssemblyResolver = resolver });
    var types = AllTypes(rebuilt.Types).ToDictionary(t => t.FullName);
    var allowed = new HashSet<string>();
    int unchanged = 0;
    foreach (var type in AllTypes(original.Types))
    foreach (var method in type.Methods)
    {
        var newer = types[type.FullName].Methods.Single(m => m.FullName == method.FullName);
        string Dump(MethodDefinition m) => m.HasBody ? string.Join("\n", m.Body.Instructions.Select(i => i.ToString())) : "no body";
        if (Dump(method) == Dump(newer)) { unchanged++; continue; }
        bool expected = (type.FullName == "PCL.PageDownloadInstall" && method.Name == "LoaderInit")
            || (type.FullName == "PCL.ModModpack" && method.Name == "ModpackInstall")
            || type.FullName == "PCL.ModModpack/_Closure$__0-0";
        if (!expected) throw new Exception("Unexpected modified method: " + method.FullName);
        allowed.Add(method.FullName);
        Console.WriteLine("Changed method: " + method.FullName);
    }
    foreach (var resource in original.Resources.OfType<EmbeddedResource>())
    {
        var current = rebuilt.Resources.OfType<EmbeddedResource>().Single(r => r.Name == resource.Name);
        if (!resource.GetResourceData().SequenceEqual(current.GetResourceData())) throw new Exception("Changed resource " + resource.Name);
    }
    Console.WriteLine("Unchanged method bodies=" + unchanged);
    Console.WriteLine("All embedded resources unchanged=" + original.Resources.OfType<EmbeddedResource>().Count());
    Console.WriteLine("Restriction constant=" + types["PCL.ModSecret"].Fields.Single(f => f.Name == "RestrictMcDownload").Constant);
    Console.WriteLine("Assembly refs=" + string.Join(", ", rebuilt.AssemblyReferences.Select(a => a.Name)));
    Console.WriteLine("PASS");
    return;
}

using var donor = ModuleDefinition.ReadModule(args[2], new ReaderParameters { AssemblyResolver = resolver });
var oldTypes = AllTypes(original.Types).ToDictionary(t => t.FullName);
var donorTypes = AllTypes(donor.Types).ToDictionary(t => t.FullName);
var changed = new HashSet<string>();
var pending = new Queue<(MethodDefinition Source, MethodDefinition Target)>();

TypeReference MapType(TypeReference tr)
{
    if (tr is GenericParameter) return tr;
    if (tr is GenericInstanceType gi)
    {
        var result = new GenericInstanceType(MapType(gi.ElementType));
        foreach (var a in gi.GenericArguments) result.GenericArguments.Add(MapType(a));
        return result;
    }
    if (tr is ArrayType at) return new ArrayType(MapType(at.ElementType), at.Rank);
    if (tr is ByReferenceType br) return new ByReferenceType(MapType(br.ElementType));
    if (tr is PointerType ptr) return new PointerType(MapType(ptr.ElementType));
    if (oldTypes.TryGetValue(tr.FullName, out var found)) return found;
    // Only the affected closures may need newly generated types.
    if (donorTypes.TryGetValue(tr.FullName, out var td))
    {
        if (!(td.FullName.StartsWith("PCL.PageDownloadInstall/") || td.FullName.StartsWith("PCL.ModModpack/")))
            throw new InvalidOperationException("Unexpected missing local type: " + td.FullName);
        var parent = (TypeDefinition)MapType(td.DeclaringType);
        var clone = new TypeDefinition(td.Namespace, td.Name, td.Attributes, td.BaseType is null ? null : MapType(td.BaseType));
        parent.NestedTypes.Add(clone);
        oldTypes.Add(td.FullName, clone);
        foreach (var f in td.Fields) clone.Fields.Add(new FieldDefinition(f.Name, f.Attributes, MapType(f.FieldType)));
        foreach (var m in td.Methods) EnsureMethod(m);
        return clone;
    }
    return original.ImportReference(tr);
}

MethodDefinition EnsureMethod(MethodDefinition source)
{
    var targetType = (TypeDefinition)MapType(source.DeclaringType);
    var target = targetType.Methods.FirstOrDefault(m => m.FullName == source.FullName);
    if (target is null)
    {
        target = new MethodDefinition(source.Name, source.Attributes, MapType(source.ReturnType)) { ImplAttributes = source.ImplAttributes };
        foreach (var p in source.Parameters) target.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, MapType(p.ParameterType)));
        targetType.Methods.Add(target);
    }
    if (changed.Add(source.FullName)) pending.Enqueue((source, target));
    return target;
}

MethodReference MapMethod(MethodReference mr)
{
    if (mr is GenericInstanceMethod gm)
    {
        var r = new GenericInstanceMethod(MapMethod(gm.ElementMethod));
        foreach (var a in gm.GenericArguments) r.GenericArguments.Add(MapType(a));
        return r;
    }
    if (mr.DeclaringType is not GenericInstanceType && donorTypes.TryGetValue(mr.DeclaringType.FullName, out var dt))
    {
        var md = dt.Methods.FirstOrDefault(m => m.FullName == mr.FullName);
        if (md is null) throw new InvalidOperationException("Unknown donor method " + mr.FullName);
        // New code can use changed lambda bodies even where their names existed already.
        if (dt.FullName.Contains("/") && (dt.FullName.StartsWith("PCL.PageDownloadInstall/") || dt.FullName.StartsWith("PCL.ModModpack/")))
            return EnsureMethod(md);
        var mappedType = (TypeDefinition)MapType(mr.DeclaringType);
        var existing = mappedType.Methods.FirstOrDefault(m => m.FullName == mr.FullName);
        return existing ?? throw new InvalidOperationException("Missing original method " + mr.FullName);
    }
    var result = new MethodReference(mr.Name, MapType(mr.ReturnType), MapType(mr.DeclaringType))
    { HasThis = mr.HasThis, ExplicitThis = mr.ExplicitThis, CallingConvention = mr.CallingConvention };
    foreach (var g in mr.GenericParameters) result.GenericParameters.Add(new GenericParameter(g.Name, result));
    foreach (var p in mr.Parameters) result.Parameters.Add(new ParameterDefinition(MapType(p.ParameterType)));
    return original.ImportReference(result);
}

FieldReference MapField(FieldReference fr)
{
    if (fr.DeclaringType is not GenericInstanceType && donorTypes.ContainsKey(fr.DeclaringType.FullName))
    {
        var td = (TypeDefinition)MapType(fr.DeclaringType);
        var found = td.Fields.FirstOrDefault(f => f.Name == fr.Name);
        if (found is null)
        {
            if (!td.FullName.Contains("/")) throw new InvalidOperationException("Missing field " + fr.FullName);
            var source = donorTypes[fr.DeclaringType.FullName].Fields.First(f => f.Name == fr.Name);
            found = new FieldDefinition(fr.Name, source.Attributes, MapType(fr.FieldType));
            td.Fields.Add(found);
        }
        return found;
    }
    return original.ImportReference(new FieldReference(fr.Name, MapType(fr.FieldType), MapType(fr.DeclaringType)));
}

foreach (var name in new[] { "PCL.PageDownloadInstall", "PCL.ModModpack" })
foreach (var m in donorTypes[name].Methods.Where(m => m.Name is "LoaderInit" or "ModpackInstall")) EnsureMethod(m);

while (pending.TryDequeue(out var item))
{
    var (source, target) = item;
    if (!source.HasBody) continue;
    var body = new MethodBody(target) { InitLocals = source.Body.InitLocals, MaxStackSize = source.Body.MaxStackSize };
    target.Body = body;
    var variables = source.Body.Variables.ToDictionary(v => v, v => new VariableDefinition(MapType(v.VariableType)));
    foreach (var v in source.Body.Variables) body.Variables.Add(variables[v]);
    var instructions = source.Body.Instructions.ToDictionary(i => i, i => Instruction.Create(OpCodes.Nop));
    foreach (var i in source.Body.Instructions)
    {
        var ni = instructions[i];
        ni.OpCode = i.OpCode;
        ni.Operand = i.Operand switch
        {
            Instruction targetInstruction => instructions[targetInstruction],
            Instruction[] targets => targets.Select(t => instructions[t]).ToArray(),
            VariableDefinition v => variables[v],
            ParameterDefinition p => p.Index < 0 ? body.ThisParameter : target.Parameters[p.Index],
            TypeReference t => MapType(t),
            MethodReference m => MapMethod(m),
            FieldReference f => MapField(f),
            CallSite => throw new InvalidOperationException("Unexpected callsite"),
            _ => i.Operand
        };
        body.Instructions.Add(ni);
    }
    Instruction? MapInstruction(Instruction? i) => i is null ? null : instructions[i];
    foreach (var eh in source.Body.ExceptionHandlers)
        body.ExceptionHandlers.Add(new ExceptionHandler(eh.HandlerType)
        {
            TryStart = MapInstruction(eh.TryStart), TryEnd = MapInstruction(eh.TryEnd),
            HandlerStart = MapInstruction(eh.HandlerStart), HandlerEnd = MapInstruction(eh.HandlerEnd),
            FilterStart = MapInstruction(eh.FilterStart), CatchType = eh.CatchType is null ? null : MapType(eh.CatchType)
        });
    Console.WriteLine("Restored " + source.FullName);
}
oldTypes["PCL.ModSecret"].Fields.Single(f => f.Name == "RestrictMcDownload").Constant = false;
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);
original.Write(args[3]);
Console.WriteLine("Output " + args[3]);
