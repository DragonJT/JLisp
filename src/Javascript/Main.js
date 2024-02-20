
function Parser(code){
    var index = 0;

    function IsWhitespace(c){
        return c==' ' || c=='\t' || c=='\n' || c=='\r';
    }

    function Parse(){
        var result = [];
        while(true){
            if(index>=code.length){
                return result;
            }
            else if(code[index] == ')'){
                index++;
                return result;
            }
            else if(code[index] == '('){
                index++;
                result.push(Parse());
            }
            else if(IsWhitespace(code[index])){
                index++;
            }
            else if(code[index] == '"'){
                var start = index;
                index++;
                while(true){
                    if(code[index] == '"'){
                        index++;
                        result.push(code.substring(start, index));
                        break;
                    }
                    index++;
                }
            }
            else{
                var start = index;
                while(true){
                    var breakVarname = index>=code.length
                        || code[index] == '('
                        || code[index] == ')'
                        || code[index] == '"'
                        || IsWhitespace(code[index]);
                    if(breakVarname){
                        result.push(code.substring(start, index));
                        break;
                    }
                    index++;
                }
            }
        }
    }

    return Parse();
}

function CompileAndRun(main){
//#WasmEmitter.js
    var tree = Parser(lispProgram);
    var importFunctions = [];
    var functions = [];
    var errors = [];
    var sections = {};

    function FindFunctions(){
        function AddFunction(v, _export){
            if(!(v.length > 3)){
                errors.push("export function expecting > 4 values: "+JSON.stringify(v));
            }
            else{
                functions.push({returnType:v[1], export:_export, name:v[2], parameters:v[3], body:v.slice(4)});
            }
        }

        for(var v of tree){
            
            if(v[0] == "import_fn"){
                if(v.length != 4) {
                    errors.push("import function expecting 4 values: "+JSON.stringify(v));
                }
                else{
                    importFunctions.push({returnType:v[1], name:v[2], parameters:v[3], javascript:v[4]});
                }
            }
            else if(v[0] == "export_fn"){
                AddFunction(v, true);
            }
            else if(v[0] == "fn"){
                AddFunction(v, false);
            }
            else{
                errors.push("Unknown object: "+JSON.stringify(v));
            }
        }
    }

    function FindParameters(){
        function ConvertParameters(f){
            if(!Array.isArray(f.parameters)){
                errors.push("parameters isnt an array: "+JSON.stringify(f));
            }
            else if(f.parameters%2 != 0){
                errors.push("parameters length cannot be divided by 2: "+JSON.stringify(f));
            }
            else{
                var result = [];
                for(var i=0;i<f.parameters.length;i+=2){
                    result.push({type:f.parameters[i], name:f.parameters[i+1]});
                }
                return result;
            }
        }

        for(var f of importFunctions){
            f.parameters = ConvertParameters(f);
        }
        for(var f of functions){
            f.parameters = ConvertParameters(f);
        }
    }

    function SetFunctionIDs(){
        for(var i=0;i<importFunctions.length;i++){
            importFunctions[i].id = i;
        }
        for(var i=0;i<functions.length;i++){
            functions[i].id = i+importFunctions.length;
        }
    }

    function EmitTypeSection(){
        function GetValtype(typeName){
            switch(typeName){
                case 'float': return Valtype.f32;
                case 'int': return Valtype.i32;
                default: errors.push("Unexpected valtype: "+typeName);
            }
        }
    
        function GetReturnArray(returnType){
            if(returnType == 'void')
                return [];
            else{
                return [GetValtype(returnType)];
            }
        }

        function EmitTypes(functions){
            return functions.map(f=>[
                functionType,
                ...encodeVector(f.parameters.map(p=>GetValtype(p.type))),
                ...encodeVector(GetReturnArray(f.returnType)),
            ]);
        }
        sections.type = createSection(Section.type, encodeVector([...EmitTypes(importFunctions), ...EmitTypes(functions)]));
    }

    function EmitImportSection(){
        function EmitImportFunctions(){
            return importFunctions.map((f,i)=>[
                ...encodeString("env"),
                ...encodeString(f.name),
                ExportType.func,
                ...unsignedLEB128(i)
            ]);
        }

        sections.import = createSection(Section.import, encodeVector([...EmitImportFunctions(), memoryImport]));
    }

    function EmitFuncSection(){
        sections.func = createSection(Section.func, encodeVector(functions.map(f=>unsignedLEB128(f.id))));
    }

    function EmitExportSection(){
        sections.export = createSection(
            Section.export,
            encodeVector(functions.filter((f)=>f.export).map(f=>[...encodeString(f.name), ExportType.func, ...unsignedLEB128(f.id)])),
        );
    }

    function EmitCodeSection(){
//#EmitFunction.js
        sections.code = createSection(Section.code, encodeVector(functions.map(f=>EmitFunction(f))));
    }

    function Run(){
        function ImportObject(){
            var code = "var importObject = {env:{}};\n";
            code+="var global = {};\n";
            for(var f of importFunctions){
                code+="importObject.env."+f.name+"= (";
                for(var i=0;i<f.parameters.length;i++){
                    code+=f.parameters[i].name;
                    if(i<f.parameters.length-1)
                        code+=',';
                }
                code+=")=>{"
                code+=f.javascript;
                code+="};\n";
            }
            code+="return importObject;\n"
            return new Function('exports', code)(exports);
        }

        const wasm = Uint8Array.from([
            ...magicModuleHeader,
            ...moduleVersion,
            ...sections.type,
            ...sections.import,
            ...sections.func,
            ...sections.export,
            ...sections.code,
        ]);

        var exports = {};
        var importObject = ImportObject();
        importObject.env.memory = new WebAssembly.Memory({ initial: 10, maximum: 10 });
        WebAssembly.instantiate(wasm, importObject).then(
            (obj) => {
                for(var f of functions){
                    if(f.export){
                        exports[f.name] = obj.instance.exports[f.name];
                    }
                }
                console.log(obj.instance.exports[main]());
            }
        );
    }
        

    function Stages(funcs){
        for(var f of funcs){
            f();
            if(errors.length>0){
                console.log(errors);
                return;
            }
        }
    }

    Stages([FindFunctions, FindParameters, SetFunctionIDs, EmitTypeSection, 
        EmitImportSection, EmitFuncSection, EmitExportSection, EmitCodeSection, Run]);
}
CompileAndRun("Main");

