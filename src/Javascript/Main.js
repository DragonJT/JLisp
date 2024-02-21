

function CompileAndRun(main){
//#Parse.js
//#WasmEmitter.js
    var errors = [];

    var expression = new Or();

    var add = new Obj('+', [new Literal('+')], new Params('expressions', expression, 2));

    var call = new Obj('call', [['name', new Varname()]], new Params('args', expression));

    expression.Init([new Varname(), new Int(), new String(), add, call]);

    var _return = new Obj('return', [new Literal('return')], new Params('expressions', expression, 0, 1));

    var assign = new Obj('=', [
        new Literal('='),
        ['name', new Varname()],
        ['expression', expression]]);

    var body = new Or([_return, assign, call]);

    var parameters = new ArrayMultipleOf2(['type', new Varname()], ['name', new Varname()]);

    var int = new Obj('int', [new Literal('int')], new Params('variables', new Varname()));

    var fn = new Obj('fn', [
        ['returnType', new Varname()],
        ['name', new Varname()], 
        ['parameters', parameters],
        ['int', int]
        ], 
        new Params('body', body));

    var importFn = new Obj('fn', [
        ['returnType', new Varname()],
        ['name', new Varname()], 
        ['parameters', parameters],
        ['javascript', new String()]
        ]);

    var exports = new Obj('exports', [new Literal('exports')], new Params('body', fn));

    var nonExports = new Obj('nonExports', [new Literal('nonExports')], new Params('body', fn));

    var imports = new Obj('imports', [new Literal('imports')], new Params('body', importFn));

    var base = new Obj('base', [['imports',imports],['nonExports', nonExports],['exports', exports]]);

    var lispTree = LispParser(lispProgram);
    if(!base.Check(lispTree)){
        throw "Cannot begin parse";
    }
    var tree = base.Parse(lispTree);

    if(errors.length>0){
        for(var e of errors){
            console.log(e);
        }
        throw "Parsing errors";
    }

    var id = 0;
    for(var f of tree.imports.body){
        f.id = id;
        id++;
    }
    for(var f of tree.nonExports.body){
        f.id = id;
        id++;
    }
    for(var f of tree.exports.body){
        f.id = id;
        id++;
    }

    function EmitTypeSection(){
        function GetValtype(typeName){
            switch(typeName){
                case 'float': return Valtype.f32;
                case 'int': return Valtype.i32;
                default: throw "Unexpected valtype: "+typeName;
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
        return createSection(Section.type, encodeVector([
            ...EmitTypes(tree.imports.body), ...EmitTypes(tree.nonExports.body), ...EmitTypes(tree.exports.body)]));
    }

    function EmitImportSection(){
        function EmitImportFunctions(){
            return tree.imports.body.map((f,i)=>[
                ...encodeString("env"),
                ...encodeString(f.name),
                ExportType.func,
                ...unsignedLEB128(i)
            ]);
        }

        return createSection(Section.import, encodeVector([...EmitImportFunctions(), memoryImport]));
    }
    
    function EmitFuncSection(){
        return createSection(Section.func, 
            encodeVector([
                ...tree.nonExports.body.map(f=>unsignedLEB128(f.id)),
                ...tree.exports.body.map(f=>unsignedLEB128(f.id))]));
    }

    function EmitExportSection(){
       return createSection(
            Section.export,
            encodeVector(tree.exports.body.map(f=>[...encodeString(f.name), ExportType.func, ...unsignedLEB128(f.id)])),
        );
    }

    function EmitCodeSection(){
//#EmitFunction.js
        return createSection(Section.code, encodeVector(
            [...tree.nonExports.body.map(f=>EmitFunction(tree, f)), ...tree.exports.body.map(f=>EmitFunction(tree, f))]));
    }

    function ImportObject(){
        var code = "var importObject = {env:{}};\n";
        code+="var global = {};\n";
        for(var f of tree.imports.body){
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
        code+="return importObject;\n";
        return new Function('exports', code)(exports);
    }

    const wasm = Uint8Array.from([
        ...magicModuleHeader,
        ...moduleVersion,
        ...EmitTypeSection(),
        ...EmitImportSection(),
        ...EmitFuncSection(),
        ...EmitExportSection(),
        ...EmitCodeSection(),
    ]);

    var exports = {};
    var importObject = ImportObject();
    importObject.env.memory = new WebAssembly.Memory({ initial: 10, maximum: 10 });
    WebAssembly.instantiate(wasm, importObject).then(
        (obj) => {
            for(var f of tree.exports.body){
                exports[f.name] = obj.instance.exports[f.name];
            }
            console.log(obj.instance.exports[main]());
        }
    );
}
CompileAndRun("Main");

