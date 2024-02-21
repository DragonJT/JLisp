function EmitFunction(functionFinder, f){
    var wasm = [];
    var i32Count = 0;
    var variables = {};
    var id = 0;
    for(var v of f.parameters){
        variables[v.name]= {id, type:v.type};
        id++;
    }

    function GetFunction(name){
        var f = functionFinder[name];
        if(name == undefined){
            throw "cant find function: "+name;
        }
        return f;
    }

    function GetVar(name){
        var v = variables[name];
        if(v==undefined){
            throw "cant find variable: "+name;
        }
        return v;
    }

    function FindLocalsInBody(body){
        for(var s of body){
            if(s.type == 'var'){
                var type = GetTypeFromExpression(s.value);
                variables[s.name] = {id, type};
                i32Count++;
                id++;
            }
            else if(s.type == 'if'){
                FindLocalsInBody(s.body);
            }
            else if(s.type == 'loop'){
                FindLocalsInBody(s.body);
            }
        }
    }
    FindLocalsInBody(f.body);

    var breakDepth = 0;

    function EmitCall(call){
        var func = GetFunction(call.name);

        if(func.parameters.length != call.args.length){
            throw "function "+func.name+" expects "+func.parameters.length+" args. got: "+call.args.length; 
        }
        for(var i=0;i<call.args.length;i++){
            var arg = call.args[i];
            var type = GetTypeFromExpression(arg);
            if(type != func.parameters[i].type){
                throw "function "+func.name+" expects argument "+i+" of type "+func.parameters[i].type+". got: "+type;
            }
        }

        for(var arg of call.args){
            EmitExpression(arg);
        }
        wasm.push(Opcode.call, ...unsignedLEB128(func.id));
    }

    function GetTypeFromExpression(expression){
        if(expression.type == 'Varname'){
            return GetVar(expression.value).type; 
        }
        else if(expression.type == 'Int'){
            return 'int';
        }
        else if(expression.type == 'Float'){
            return 'float';
        }
        else if(expression.type == '+'){
            return GetTypeFromExpressions(expression.values);
        }
        else if(expression.type == '*'){
            return GetTypeFromExpressions(expression.values);
        }
        else if(expression.type == '/'){
            return GetTypeFromExpressions(expression.values);
        }
        else if(expression.type == '-'){
            return GetTypeFromExpressions(expression.values);
        }
        else if(expression.type == '<'){
            return 'bool';
        }
        else if(expression.type == '>'){
            return 'bool';
        }
        else if(expression.type == 'call'){
            return GetFunction(expression.name).returnType;
        }
        else{
            throw "Cant get type from: "+JSON.stringify(expression);
        }
    }

    function GetTypeFromExpressions(expressions){
        var type = GetTypeFromExpression(expressions[0]);
        for(var i=1;i<expressions.length;i++){
            var nextType = GetTypeFromExpression(expressions[i]);
            if(nextType != type){
                throw "Expecting all types to be the same: "+JSON.stringify(expressions);
            }
        }
        return type;
    }

    function EmitOperatorNumbersInBoolOut(expression, op){
        var stackSize = 0;
        for(var i=0;i<expression.values.length-1;i++){
            EmitExpression(expression.values[i]);
            EmitExpression(expression.values[i+1]);
            wasm.push(op);
            stackSize++;
        }
        while(stackSize>1){
            wasm.push(Opcode.i32_and);
            stackSize--;
        }
    }

    function EmitArithmenticOperator(expression, ops){
        var type = GetTypeFromExpressions(expression.values);
        var op = ops[type];
        for(var i=0;i<expression.values.length;i++){
            EmitExpression(expression.values[i]);
            if(i>0){
                wasm.push(op);
            }
        }
    }

    function EmitExpression(expression){
        if(expression.type == 'Varname'){
            wasm.push(Opcode.get_local, GetVar(expression.value).id);
        }
        else if(expression.type == 'Int'){
            wasm.push(Opcode.i32_const, ...signedLEB128(expression.value));
        }
        else if(expression.type == 'Float'){
            wasm.push(Opcode.f32_const, ...ieee754(expression.value));
        }
        else if(expression.type == '+'){
            EmitArithmenticOperator(expression, {int:Opcode.i32_add, float:Opcode.f32_add});
        }
        else if(expression.type == '*'){
            EmitArithmenticOperator(expression, {int:Opcode.i32_mul, float:Opcode.f32_mul});
        }
        else if(expression.type == '/'){
            EmitArithmenticOperator(expression, {int:Opcode.i32_div, float:Opcode.f32_div});
        }
        else if(expression.type == '-'){
            EmitArithmenticOperator(expression, {int:Opcode.i32_sub, float:Opcode.f32_sub});
        }
        else if(expression.type == '<'){
            EmitOperatorNumbersInBoolOut(expression, Opcode.i32_lt);
        }
        else if(expression.type == '>'){
            EmitOperatorNumbersInBoolOut(expression, Opcode.i32_gt);
        }
        else if(expression.type == 'call'){
            EmitCall(expression);
        }
        else{
            throw "Unexpected expression: "+JSON.stringify(expression);
        }
    }

    function EmitStatement(statement){
        if(statement.type == '='){
            EmitExpression(statement.value);
            wasm.push(Opcode.set_local, GetVar(statement.name).id);
        }
        else if(statement.type == 'return'){
            if(statement.values.length == 1){
                EmitExpression(statement.values[0]);
            }
            wasm.push(Opcode.return);
        }
        else if(statement.type == 'if'){
            EmitExpression(statement.condition);
            wasm.push(Opcode.if, Blocktype.void);
            EmitBody(statement.body);
            wasm.push(Opcode.end);
        }
        else if(statement.type == 'loop'){
            wasm.push(Opcode.block, Blocktype.void);
            wasm.push(Opcode.loop, Blocktype.void);
            breakDepth = 0;
            EmitBody(statement.body);
            wasm.push(Opcode.br, ...unsignedLEB128(0));
            wasm.push(Opcode.end);
            wasm.push(Opcode.end);
        }
        else if(statement.type == 'break'){
            wasm.push(Opcode.br, ...unsignedLEB128(breakDepth));
        }
        else if(statement.type == 'call'){
            EmitCall(statement);
        }
        else if(statement.type == 'var'){
            EmitExpression(statement.value);
            wasm.push(Opcode.set_local, GetVar(statement.name).id);
        }
        else{
            throw "Unexpected statement: "+JSON.stringify(statement);
        }
    }

    function EmitBody(body){
        breakDepth++;
        for(var s of body){
            EmitStatement(s);
        }
        breakDepth--;
    }
    
    EmitBody(f.body);
    return encodeVector([...encodeVector([encodeLocal(i32Count, Valtype.i32)]), ...wasm, Opcode.end])
}