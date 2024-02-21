function EmitFunction(functionFinder, f){
    var wasm = [];
    var i32Count = 0;
    var variables = {};
    var id = 0;
    for(var v of f.parameters){
        variables[v.name]= {id};
        id++;
    }

    function FindLocalsInBody(body){
        for(var s of body){
            if(s.type == 'var'){
                variables[s.name] = {id};
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
        for(var a of call.args){
            EmitExpression(a);
        }
        wasm.push(Opcode.call, ...unsignedLEB128(functionFinder[call.name].id));
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

    function EmitArithmenticOperator(expression, op){
        for(var i=0;i<expression.values.length;i++){
            EmitExpression(expression.values[i]);
            if(i>0){
                wasm.push(op);
            }
        }
    }

    function EmitExpression(expression){
        if(expression.type == 'Varname'){
            wasm.push(Opcode.get_local, variables[expression.value].id);
        }
        else if(expression.type == 'Int'){
            wasm.push(Opcode.i32_const, ...signedLEB128(expression.value));
        }
        else if(expression.type == '+'){
            EmitArithmenticOperator(expression, Opcode.i32_add);
        }
        else if(expression.type == '*'){
            EmitArithmenticOperator(expression, Opcode.i32_mul);
        }
        else if(expression.type == '/'){
            EmitArithmenticOperator(expression, Opcode.i32_div);
        }
        else if(expression.type == '-'){
            EmitArithmenticOperator(expression, Opcode.i32_sub);
        }
        else if(expression.type == '<'){
            EmitOperatorNumbersInBoolOut(expression, Opcode.i32_lt);
        }
        else if(expression.type == '>'){
            EmitOperatorNumbersInBoolOut(expression, Opcode.i32_gt)
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
            wasm.push(Opcode.set_local, variables[statement.name].id);
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
            wasm.push(Opcode.set_local, variables[statement.name].id);
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