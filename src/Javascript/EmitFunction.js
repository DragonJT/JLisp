function EmitFunction(functionFinder, f){
    var wasm = [];
    var i32Count = f.int.variables.length;

    var variables = {};
    var id = 0;
    for(var v of f.parameters){
        variables[v.name]= {id};
        id++;
    }
    for(var v of f.int.variables){
        variables[v] = {id};
        id++;
    }

    function EmitCall(call){
        for(var a of call.args){
            EmitExpression(a);
        }
        wasm.push(Opcode.call, ...unsignedLEB128(functionFinder[call.name].id));
    }

    function EmitArithmenticOperator(expression, op){
        for(var i=0;i<expression.expressions.length;i++){
            EmitExpression(expression.expressions[i]);
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
        else if(expression.type == 'call'){
            EmitCall(expression);
        }
        else{
            throw "Unexpected expression: "+JSON.stringify(expression);
        }
    }

    function EmitStatement(statement){
        if(statement.type == '='){
            EmitExpression(statement.expression);
            wasm.push(Opcode.set_local, variables[statement.name].id);
        }
        else if(statement.type == 'return'){
            if(statement.expressions.length == 1){
                EmitExpression(statement.expressions[0]);
            }
            wasm.push(Opcode.return);
        }
        else if(statement.type == 'call'){
            EmitCall(statement);
        }
        else{
            throw "Unexpected statement: "+JSON.stringify(statement);
        }
    }

    for(var s of f.body){
        EmitStatement(s);
    }
    return encodeVector([...encodeVector([encodeLocal(i32Count, Valtype.i32)]), ...wasm, Opcode.end])
}