function EmitFunction(f){
    var wasm = [];
    var i32Count = f.i32.variables.length;
    var locals = {};
    var id = 0;
    for(var v of f.i32.variables){
        locals[v] = {id};
        id++;
    }

    function EmitExpression(expression){
        if(expression.type == 'Varname'){
            wasm.push(Opcode.get_local, locals[expression.value].id);
            return;
        }
        else if(expression.type == 'Int'){
            wasm.push(Opcode.i32_const, ...signedLEB128(expression.value));
            return;
        }
        else if(expression.type == '+'){
            for(var i=0;i<expression.expressions.length;i++){
                EmitExpression(expression.expressions[i]);
                if(i>0){
                    wasm.push(Opcode.i32_add);
                }
            }
        }
        else{
            throw "Unexpected expression: "+JSON.stringify(expression);
        }
    }

    function EmitStatement(statement){
        if(statement.type == '='){
            EmitExpression(statement.expression);
            wasm.push(Opcode.set_local, locals[statement.name].id);
        }
        else if(statement.type == 'return'){
            if(statement.expressions.length == 1){
                EmitExpression(statement.expressions[0]);
            }
            wasm.push(Opcode.return);
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