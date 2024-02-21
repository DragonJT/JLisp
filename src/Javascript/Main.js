

function CompileAndRun(main, code){
//#Parse.js
//#WasmEmitter.js
    var errors = [];

    var expression = new Or();

    var add = new Obj('+', [new Literal('+')], new Params('expressions', expression, 2));

    var mul = new Obj('*', [new Literal('*')], new Params('expressions', expression, 2));

    var div = new Obj('/', [new Literal('/')], new Params('expressions', expression, 2));

    var sub = new Obj('-', [new Literal('-')], new Params('expressions', expression, 2));

    var call = new Obj('call', [['name', new Varname()]], new Params('args', expression));

    expression.Init([new Varname(), new Int(), new String(), add, mul, div, sub, call]);

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

    var exportFn = new Obj('exportFn', [
        new Literal('export'),
        ['returnType', new Varname()],
        ['name', new Varname()], 
        ['parameters', parameters],
        ['int', int]
        ], 
        new Params('body', body));

    var importFn = new Obj('importFn', [
        new Literal('import'),
        ['returnType', new Varname()],
        ['name', new Varname()], 
        ['parameters', parameters],
        ['javascript', new String()]
        ]);

    var base = new Obj('base', [], new Params('values', new Or([importFn, exportFn, fn])));

    var lispTree = LispParser(code);
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

    var importFunctions = tree.values.filter(v=>v.type == 'importFn');
    var nonExportFunctions = tree.values.filter(v=>v.type == 'fn');
    var exportFunctions = tree.values.filter(v=>v.type == 'exportFn');
    var functions = [...nonExportFunctions, ...exportFunctions];
    var allFunctions = [...importFunctions, ...functions];
    var functionFinder = {};

    for(var i=0;i<allFunctions.length;i++){
        var f = allFunctions[i];
        functionFinder[f.name] = f;
        f.id = i;
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
        return createSection(Section.type, encodeVector(EmitTypes(allFunctions)));
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

        return createSection(Section.import, encodeVector([...EmitImportFunctions(), memoryImport]));
    }
    
    function EmitFuncSection(){
        return createSection(Section.func, encodeVector(functions.map(f=>unsignedLEB128(f.id))));
    }

    function EmitExportSection(){
       return createSection(
            Section.export,
            encodeVector(exportFunctions.map(f=>[...encodeString(f.name), ExportType.func, ...unsignedLEB128(f.id)])),
        );
    }

    function EmitCodeSection(){
//#EmitFunction.js
        return createSection(Section.code, encodeVector(functions.map(f=>EmitFunction(functionFinder, f))));
    }

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
            for(var f of exportFunctions){
                exports[f.name] = obj.instance.exports[f.name];
            }
            console.log(exports[main]());
        }
    );
}

function CreateUI(){
    var textarea = document.createElement('textarea');
    document.body.appendChild(textarea);
    textarea.value = lispProgram;
    textarea.rows = 15;
    textarea.cols = 60;
    textarea.onkeydown = (e)=> {
        if (e.key == "Tab") {
            e.preventDefault();
        
            const start = textarea.selectionStart;
            const end = textarea.selectionEnd;
        
            textarea.value = textarea.value.substring(0, start) + "     " + textarea.value.substring(end);
        
            textarea.selectionStart = textarea.selectionEnd = start + 4;
        }
    }

    var runbutton = document.createElement('button');
    document.body.appendChild(runbutton);
    runbutton.innerHTML = 'run';
    runbutton.onclick = ()=>{CompileAndRun('Main', textarea.value)}
}

CreateUI();
CompileAndRun("Main", lispProgram);

