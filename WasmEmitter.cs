
static class WasmEmitter{

    class ImportFunction(LispToken returnType, string name, LispToken parameters, string javascript){
        public LispToken returnType = returnType;
        public string name = name;
        public LispToken parameters = parameters;
        public string javascript = javascript;
    }

    class Function(LispToken returnType, bool export, string name, LispToken parameters, LispToken[] instructions){
        public LispToken returnType = returnType;
        public bool export = export;
        public string name = name;
        public LispToken parameters = parameters;
        public LispToken[] instructions = instructions;
    }

    static string GetInitFunction(List<Function> functions){
        string initialInstructions = "";
        var memLocation = 0;
        foreach(var function in functions){
            foreach(var instruction in function.instructions){
                var name = instruction.GetName();
                if(name == "string.const"){
                    var value = instruction.GetTokens(name.Length)[0].value;
                    instruction.value = "i32.const "+memLocation;
                    initialInstructions += $"(i32.const {memLocation}) (i32.const {value.Length}) (i32.store)";
                    memLocation+=4;
                    foreach(var c in value){
                        initialInstructions += $"(i32.const {memLocation}) (i32.const {(int)c}) (i32.store_8)";
                        memLocation++;
                    }
                    memLocation+=4-(memLocation%4);
                }
            }
        }
        return initialInstructions;
    }

    static byte[] GetFunctionWasm(Function function){
        var localIDs = new Dictionary<string, uint>();
        foreach(var instruction in function.instructions){
            var name = instruction.GetName();
            if(name == "create_local"){
                var localname = instruction.GetTokens(name.Length)[0].value;
                var localID = (uint)localIDs.Count;
                localIDs.Add(localname, localID);
                instruction.value = "set_local "+localname;
            }
        }
        var localBytes = WasmHelper.Local((uint)localIDs.Count, Valtype.I32);

        List<byte> codeBytes = [];
        foreach(var instruction in function.instructions){
            var name = instruction.GetName();
            if(name == "i32.const"){
                var value = int.Parse(instruction.GetTokens(name.Length)[0].value);
                codeBytes.AddRange([(byte)Opcode.i32_const, .. WasmHelper.SignedLEB128(value)]);
            }
            else if(name == "i32.store_8"){
                codeBytes.AddRange([(byte)Opcode.i32_store_8, 0, 0]); // align and offset
            }
            else if(name == "i32.store"){
                codeBytes.AddRange([(byte)Opcode.i32_store, 0, 0]); // align and offset
            }
            else if(name == "get_local"){
                var localID = localIDs[instruction.GetTokens(name.Length)[0].value];
                codeBytes.AddRange([(byte)Opcode.get_local, .. WasmHelper.UnsignedLEB128(localID)]);
            }
            else if(name == "set_local"){
                var localID = localIDs[instruction.GetTokens(name.Length)[0].value];
                codeBytes.AddRange([(byte)Opcode.set_local, .. WasmHelper.UnsignedLEB128(localID)]);
            }
            else if(name == "i32.add"){
                codeBytes.Add((byte)Opcode.i32_add);
            }
            else if(name == "i32.sub"){
                codeBytes.Add((byte)Opcode.i32_sub);
            }
            else if(name == "i32.mul"){
                codeBytes.Add((byte)Opcode.i32_mul);
            }
            else if(name == "i32.div_s"){
                codeBytes.Add((byte)Opcode.i32_div_s);
            }
            else if(name == "ret"){
                codeBytes.Add((byte)Opcode.ret);
            }
            else if(name == "call"){
                var funcID = uint.Parse(instruction.GetTokens(name.Length)[0].value);
                codeBytes.AddRange([(byte)Opcode.call, .. WasmHelper.UnsignedLEB128(funcID)]);
            }
            else{
                throw new Exception(instruction.ToString());
            }
        }
        codeBytes.Add((byte)Opcode.end);
        return WasmHelper.Vector([.. WasmHelper.UnsignedLEB128(1), .. localBytes ,.. codeBytes]);
    }

    static int GetFunctionID(List<ImportFunction> importFunctions, List<Function> functions, string name){
         for(var i=0;i<importFunctions.Count;i++){
            if(importFunctions[i].name == name){
                return i;
            }
        }
        for(var i=0;i<functions.Count;i++){
            if(functions[i].name == name){
                return i+importFunctions.Count;
            }
        }
        throw new Exception("Error cannot find function with name: "+name);
    }

    static string GetImportObject(List<ImportFunction> importFunctions){
        var code = "";
        for(var i=0;i<importFunctions.Count;i++){
            var f = importFunctions[i];
            code+="imports.env."+f.name+"= (";
            var ptokens = f.parameters.GetTokens();
            var len = ptokens.Length/2;
            for(var ip=0;ip<len;ip++){
                code+=ptokens[ip*2+1].value;
                if(i<len-1){
                    code+=", ";
                }
            }
            code+=")=>{";
            code+=f.javascript;
            code+="};\n";
        }
        return code;
    }

    static Valtype GetValtype(LispToken type){
        if(type.value == "int"){
            return Valtype.I32;
        }
        else{
            throw new Exception("Unexpected type: "+type);
        }
    }

    static Valtype[] GetReturnValtypes(LispToken type){
        if(type.value == "void"){
            return [];
        }
        return [GetValtype(type)];
    }

    static Valtype[] GetValtypes(LispToken parameters){
        List<Valtype> valtypes = [];
        var tokens = parameters.GetTokens();
        for(var i=0;i<tokens.Length;i+=2){
            valtypes.Add(GetValtype(tokens[i]));
        }
        return [..valtypes];
    }
    
    static byte[] GetTypeSignatureBytes(LispToken parameters, LispToken returnType){
        return [
            WasmHelper.functionType,
            ..WasmHelper.Vector(GetValtypes(parameters).Select(v=>(byte)v).ToArray()),
            ..WasmHelper.Vector(GetReturnValtypes(returnType).Select(v=>(byte)v).ToArray())
        ];
    }

    static bool GetBool(LispToken token){
        if(token.type == LispTokenType.Varname){
            if(token.value == "t"){
                return true;
            }
            if(token.value == "f"){
                return false;
            }
        }
        throw new Exception("Expecting t or f: "+token.ToString());
    }

    public static void EmitAndRunWasm(string il, string main){
        var tokens = new LispToken(LispTokenType.Object, il).GetTokens();
        var functions = new List<Function>();
        var importFunctions = new List<ImportFunction>();
        foreach(var t in tokens){
            var type = t.GetName();
            if(type == "import"){
                var fnTokens = t.GetTokens(type.Length);
                var returnType = fnTokens[0];
                var name = fnTokens[1].value;
                var parameters = fnTokens[2];
                var javascript = fnTokens[3].ToString();
                importFunctions.Add(new ImportFunction(returnType, name, parameters, javascript));
            }
            else if(type == "fn"){
                var fnTokens = t.GetTokens(type.Length);
                var export = GetBool(fnTokens[0]);
                var returnType = fnTokens[1];
                var name = fnTokens[2].value;
                var parameters = fnTokens[3];
                var instructions = fnTokens[4].GetTokens();
                functions.Add(new Function(returnType, export, name, parameters, instructions));
            }
            else{
                throw new Exception(t.ToString());
            }
        }

        var initFunctionBody = GetInitFunction(functions);
        functions.Add(new Function(
            new LispToken(LispTokenType.Varname, "void"), 
            true, 
            "__Init__", 
            new LispToken(LispTokenType.Object, ""),
            new LispToken(LispTokenType.Object, initFunctionBody).GetTokens()));

        var importObject = GetImportObject(importFunctions);

        foreach(var f in functions){
            foreach(var i in f.instructions){
                var name = i.GetName();
                if(name == "call"){
                    var funcID = GetFunctionID(importFunctions, functions, i.GetTokens(name.Length)[0].value);
                    i.value = "call "+funcID;
                }
            }
        }

        List<byte[]> codeSection = [];
        foreach(var f in functions){
            codeSection.Add(GetFunctionWasm(f));
        }

        List<byte[]> importSection = [];
        for(var i=0;i<importFunctions.Count;i++){
            importSection.Add([
                ..WasmHelper.String("env"), 
                ..WasmHelper.String(importFunctions[i].name),
                (byte)ExportType.Func,
                ..WasmHelper.UnsignedLEB128((uint)i)]);
        }
        importSection.Add(WasmHelper.MemoryImport);

        List<byte[]> typeSection = [];
        foreach(var f in importFunctions){
            typeSection.Add(GetTypeSignatureBytes(f.parameters, f.returnType));
        }
        foreach(var f in functions){
            typeSection.Add(GetTypeSignatureBytes(f.parameters, f.returnType));
        }

        List<byte[]> funcSection = [];
        for(var i=0;i<functions.Count;i++){
            funcSection.Add(WasmHelper.UnsignedLEB128((uint)(i+importFunctions.Count)));
        }

        List<byte[]> exportSection = [];
        for(var i=0;i<functions.Count;i++){
            if(functions[i].export){
                exportSection.Add([
                    ..WasmHelper.String(functions[i].name), 
                    (byte)ExportType.Func, 
                    ..WasmHelper.UnsignedLEB128((uint)(importFunctions.Count+i))]);
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