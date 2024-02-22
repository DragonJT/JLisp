

function CompileAndRun(main, code){
//#Parse.js
//#EmitJavascript.js

    var expression = new Or();

    var add = new Obj('+', [new Literal('+')], new Params('values', expression, 2));

    var mul = new Obj('*', [new Literal('*')], new Params('values', expression, 2));

    var div = new Obj('/', [new Literal('/')], new Params('values', expression, 2));

    var sub = new Obj('-', [new Literal('-')], new Params('values', expression, 2));

    var lt = new Obj('<', [new Literal('<')], new Params('values', expression, 2, 2));

    var gt = new Obj('>', [new Literal('>')], new Params('values', expression, 2, 2));

    var call = new Obj('call', [['name', new Varname()]], new Params('args', expression));

    var obj = new ObjectMultipleOf2('obj', ['name', new Varname()], ['value', expression]);

    expression.Init([new Varname(), new Number(), new String(), obj, add, mul, div, sub, lt, gt, call]);

    var body = new Or();

    var _return = new Obj('return', [new Literal('return')], new Params('values', expression, 0, 1));

    var _var = new Obj('var', [new Literal('var'), ['name', new Varname()], ['value', expression]]);

    var _if = new Obj('if', [new Literal('if'), ['condition', expression]], new Params('body', body));

    var _break = new Obj('break', [new Literal('break')]);

    var loop = new Obj('loop',  [new Literal('loop')], new Params('body', body));

    var assign = new Obj('=', [
        new Literal('='),
        ['name', new Varname()],
        ['value', expression]]);

    var parameters = new Obj('type', [],new Params('values', new Varname()));

    var fn = new Obj('fn', [
        new Literal('fn'),
        ['name', new Varname()], 
        ['parameters', parameters]
        ], 
        new Params('body', body));

    body.Init([fn, _return, assign, _if, _var, loop, _break, call]);

    var base = new Obj('base', [], new Params('values', body));

    var lispTree = LispParser(code);
    if(!base.Check(lispTree)){
        throw "Parser failed at start";
    }
    var tree = base.Parse(lispTree);
    var javascript = EmitJavascript(tree);
    console.log(javascript);
    console.log(new Function(javascript)());
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

