enum ILOpcode{
    CreateLocal, If, End, LT, GT, I32And, I32Or, Br, BrIf, Loop, Block, I32Load, I32Load8S, I32Eqz, I32Rem,
    I32Const, StringConst, I32Store8, I32Store, GetLocal, SetLocal, I32Add, I32Sub, I32Mul, I32DivS, Ret, Call
}

class ILInstruction(ILOpcode opcode, object? value = null){
    public ILOpcode opcode = opcode;
    public object? value = value;

    public string StringValue(){
        if(value is string str){
            return str;
        }
        throw new Exception("Expecting string: "+value);
    }

    public int IntValue(){
        if(value is int i){
            return i;
        }
        throw new Exception("Expecting int: "+value);
    }

    public override string ToString(){
        var result = opcode.ToString();
        if(value!=null){
            result+=": "+value.ToString();
        }
        return result;
    }
}

class ILVariable(string type, string name){
    public string type = type;
    public string name = name;
}

class ILImportFunction(string returnType, string name, ILVariable[] parameters, string javascript){
    public string returnType = returnType;
    public string name = name;
    public ILVariable[] parameters = parameters;
    public string javascript = javascript;
}

class ILFunction(string returnType, bool export, string name, ILVariable[] parameters, ILInstruction[] instructions){
    public string returnType = returnType;
    public bool export = export;
    public string name = name;
    public ILVariable[] parameters = parameters;
    public ILInstruction[] instructions = instructions;

    public override string ToString(){
        var result = $"=============== {name} ============\n";
        foreach(var i in instructions){
            result+=i.ToString()+"\n";
        }
        return result;
    }
}

class IL{
    public List<ILImportFunction> importFunctions = [];
    public List<ILFunction> functions = [];
}

static class WasmEmitter{

    static ILInstruction[] CreateStoreInstructions(int memLocation, int value, ILOpcode storeOpcode){
        return [
            new ILInstruction(ILOpcode.I32Const, memLocation),
            new ILInstruction(ILOpcode.I32Const, value),
            new ILInstruction(storeOpcode)
        ];
    }

    static void CreateInitFunction(IL il){
        List<ILInstruction> instructions = [];
        var memLocation = 4;
        foreach(var function in il.functions){
            foreach(var instruction in function.instructions){
                if(instruction.opcode == ILOpcode.StringConst){
                    var value = instruction.StringValue();
                    instruction.opcode = ILOpcode.I32Const;
                    instruction.value = memLocation;
                    instructions.AddRange(CreateStoreInstructions(memLocation, value.Length, ILOpcode.I32Store));
                    memLocation+=4;
                    foreach(var c in value){
                        instructions.AddRange(CreateStoreInstructions(memLocation, c, ILOpcode.I32Store8));
                        memLocation++;
                    }
                    memLocation+=4-(memLocation%4);
                }
            }
        }
        instructions.AddRange(CreateStoreInstructions(0, memLocation, ILOpcode.I32Store));
        il.functions.Add(new ILFunction("void", true, "__Init__", [], [..instructions]));
    }

    static byte[] GetFunctionWasm(ILFunction function){
        var localIDs = new Dictionary<string, uint>();
        foreach(var p in function.parameters){
            localIDs.Add(p.name, (uint)localIDs.Count);
        }
        foreach(var instruction in function.instructions){
            if(instruction.opcode == ILOpcode.CreateLocal){
                var localname = instruction.StringValue();
                if(!localIDs.ContainsKey(localname)){ //warning multiple variables can have same name
                    localIDs.Add(localname, (uint)localIDs.Count);
                }
                instruction.opcode = ILOpcode.SetLocal;
            }
        }
        var localBytes = WasmHelper.Local((uint)localIDs.Count, Valtype.I32);
        List<byte> codeBytes = [];
        foreach(var instruction in function.instructions){
            var opcode = instruction.opcode;
            if(opcode == ILOpcode.If){
                codeBytes.AddRange([(byte)Opcode.@if, (byte)Blocktype.@void]);
            }
            else if(opcode == ILOpcode.End){
                codeBytes.Add((byte)Opcode.end);
            }
            else if(opcode == ILOpcode.I32And){
                codeBytes.Add((byte)Opcode.i32_and);
            }
            else if(opcode == ILOpcode.I32Or){
                codeBytes.Add((byte)Opcode.i32_or);
            }
            else if(opcode == ILOpcode.LT){
                codeBytes.Add((byte)Opcode.i32_lt_s);
            }
            else if(opcode == ILOpcode.GT){
                codeBytes.Add((byte)Opcode.i32_gt_s);
            }
            else if(opcode == ILOpcode.I32Eqz){
                codeBytes.Add((byte)Opcode.i32_eqz);
            }
            else if(opcode == ILOpcode.I32Rem){
                codeBytes.Add((byte)Opcode.i32_rem_s);
            }
            else if(opcode == ILOpcode.Br){
                codeBytes.AddRange([(byte)Opcode.br, .. WasmHelper.SignedLEB128(instruction.IntValue())]);
            }
            else if(opcode == ILOpcode.BrIf){
                codeBytes.AddRange([(byte)Opcode.br_if, .. WasmHelper.SignedLEB128(instruction.IntValue())]);
            }
            else if(opcode == ILOpcode.Loop){
                codeBytes.AddRange([(byte)Opcode.loop, (byte)Blocktype.@void]);
            }
            else if(opcode == ILOpcode.Block){
                codeBytes.AddRange([(byte)Opcode.block, (byte)Blocktype.@void]);
            }
            else if(opcode == ILOpcode.I32Const){
                codeBytes.AddRange([(byte)Opcode.i32_const, .. WasmHelper.SignedLEB128(instruction.IntValue())]);
            }
            else if(opcode == ILOpcode.I32Store8){
                codeBytes.AddRange([(byte)Opcode.i32_store_8, 0, 0]); // align and offset
            }
            else if(opcode == ILOpcode.I32Store){
                codeBytes.AddRange([(byte)Opcode.i32_store, 0, 0]); // align and offset
            }
            else if(opcode == ILOpcode.I32Load){
                codeBytes.AddRange([(byte)Opcode.i32_load, 0, 0]);
            }
            else if(opcode == ILOpcode.I32Load8S){
                codeBytes.AddRange([(byte)Opcode.i32_load8_s, 0, 0]);
            }
            else if(opcode == ILOpcode.GetLocal){
                var localID = localIDs[instruction.StringValue()];
                codeBytes.AddRange([(byte)Opcode.get_local, .. WasmHelper.UnsignedLEB128(localID)]);
            }
            else if(opcode == ILOpcode.SetLocal){
                var localID = localIDs[instruction.StringValue()];
                codeBytes.AddRange([(byte)Opcode.set_local, .. WasmHelper.UnsignedLEB128(localID)]);
            }
            else if(opcode == ILOpcode.I32Add){
                codeBytes.Add((byte)Opcode.i32_add);
            }
            else if(opcode == ILOpcode.I32Sub){
                codeBytes.Add((byte)Opcode.i32_sub);
            }
            else if(opcode == ILOpcode.I32Mul){
                codeBytes.Add((byte)Opcode.i32_mul);
            }
            else if(opcode == ILOpcode.I32Mul){
                codeBytes.Add((byte)Opcode.i32_div_s);
            }
            else if(opcode == ILOpcode.Ret){
                codeBytes.Add((byte)Opcode.ret);
            }
            else if(opcode == ILOpcode.Call){
                var funcID = (uint)instruction.IntValue();
                codeBytes.AddRange([(byte)Opcode.call, .. WasmHelper.UnsignedLEB128(funcID)]);
            }
            else{
                throw new Exception(instruction.ToString());
            }
        }
        codeBytes.Add((byte)Opcode.end);
        return WasmHelper.Vector([.. WasmHelper.UnsignedLEB128(1), .. localBytes ,.. codeBytes]);
    }

    static int GetFunctionID(IL il, string name){
         for(var i=0;i<il.importFunctions.Count;i++){
            if(il.importFunctions[i].name == name){
                return i;
            }
        }
        for(var i=0;i<il.functions.Count;i++){
            if(il.functions[i].name == name){
                return i+il.importFunctions.Count;
            }
        }
        throw new Exception("Error cannot find function with name: "+name);
    }

    static string GetParameters(ILVariable[] parameters){
        var code = "";
        for(var i=0;i<parameters.Length;i++){
            code+=parameters[i].name;
            if(i<parameters.Length-1){
                code+=", ";
            }
        }
        return code;
    }

    static string GetImportObject(List<ILImportFunction> importFunctions){
        var code = "";
        for(var i=0;i<importFunctions.Count;i++){
            var f = importFunctions[i];
            code+="imports.env."+f.name+"= (";
            code+=GetParameters(f.parameters);
            code+=")=>{";
            code+=f.javascript;
            code+="};\n";
        }
        return code;
    }

    static Valtype GetValtype(string type){
        if(type == "int"){
            return Valtype.I32;
        }
        else if(type == "string"){
            return Valtype.I32;
        }
        else{
            throw new Exception("Unexpected type: "+type);
        }
    }

    static byte[] GetReturnBytes(string type){
        if(type == "void"){
            return [];
        }
        return [(byte)GetValtype(type)];
    }

    static byte[] GetTypeSignatureBytes(ILVariable[] parameters, string returnType){
        return [
            WasmHelper.functionType,
            ..WasmHelper.Vector(parameters.Select(p=>(byte)GetValtype(p.type)).ToArray()),
            ..WasmHelper.Vector(GetReturnBytes(returnType))
        ];
    }

    public static void EmitAndRunWasm(IL il, string main){
        CreateInitFunction(il);
        var importObject = GetImportObject(il.importFunctions);

        foreach(var f in il.functions){
            foreach(var i in f.instructions){
                if(i.opcode == ILOpcode.Call){
                    i.value = GetFunctionID(il, i.StringValue());
                }
            }
        }

        List<byte[]> codeSection = [];
        foreach(var f in il.functions){
            codeSection.Add(GetFunctionWasm(f));
        }

        List<byte[]> importSection = [];
        for(var i=0;i<il.importFunctions.Count;i++){
            importSection.Add([
                ..WasmHelper.String("env"), 
                ..WasmHelper.String(il.importFunctions[i].name),
                (byte)ExportType.Func,
                ..WasmHelper.UnsignedLEB128((uint)i)]);
        }
        importSection.Add(WasmHelper.MemoryImport);

        List<byte[]> typeSection = [];
        foreach(var f in il.importFunctions){
            typeSection.Add(GetTypeSignatureBytes(f.parameters, f.returnType));
        }
        foreach(var f in il.functions){
            typeSection.Add(GetTypeSignatureBytes(f.parameters, f.returnType));
        }

        List<byte[]> funcSection = [];
        for(var i=0;i<il.functions.Count;i++){
            funcSection.Add(WasmHelper.UnsignedLEB128((uint)(i+il.importFunctions.Count)));
        }

        List<byte[]> exportSection = [];
        for(var i=0;i<il.functions.Count;i++){
            if(il.functions[i].export){
                exportSection.Add([
                    ..WasmHelper.String(il.functions[i].name), 
                    (byte)ExportType.Func, 
                    ..WasmHelper.UnsignedLEB128((uint)(il.importFunctions.Count+i))]);
            }
        }

        byte[] wasm = [
            .. WasmHelper.MagicModuleHeader,
            .. WasmHelper.ModuleVersion,
            .. WasmHelper.Section(SectionType.Type, [..typeSection]),
            .. WasmHelper.Section(SectionType.Import, [.. importSection]),
            .. WasmHelper.Section(SectionType.Func, [..funcSection]),
            .. WasmHelper.Section(SectionType.Export, [..exportSection]),
            .. WasmHelper.Section(SectionType.Code, [..codeSection])];

        string wasmString = string.Join(",", wasm.Select(b => "0x" + b.ToString("X2")));
        var html = @"
<!DOCTYPE html>
<html>
<head>
  <title>WebAssembly Example</title>
</head>
<body>
  <script>
const wasmBytecode = new Uint8Array([
" + wasmString +
@"]);
var imports = {};
imports.env = {};
" +
importObject
+ @"
imports.env.memory = new WebAssembly.Memory({ initial: 10, maximum: 10 });

function GetChar(i){
    return String.fromCharCode(i);
}

function GetString(pointer){
    const length = new Int32Array(imports.env.memory.buffer, pointer, 1)[0];
    const bytes = new Uint8Array(imports.env.memory.buffer, pointer+4, length);
    return new TextDecoder('utf8').decode(bytes);
}

WebAssembly.instantiate(wasmBytecode, imports)
  .then(module => {
    module.instance.exports.__Init__();
    console.log(module.instance.exports."+main+@"());
  })
  .catch(error => {
    console.error('Error:', error);
  });
  </script>
</body>
</html>";
        File.WriteAllText("index.html", html);
    }
}