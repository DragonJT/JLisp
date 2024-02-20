function EmitFunction(f){
    var wasm = [];
    var locals = {};
    var localCount = 0;

    function IsDigit(c){
        return c>='0' && c<='9';
    }

    function IsCharacter(c){
        return (c>='a' && c<='z') || (c>='A' && c<='Z') || c=='_';
    }

    function EmitExpression(expression){
        if(!Array.isArray(expression)){
            if(IsDigit(expression)){
                wasm.push(Opcode.i32_const, ...signedLEB128(parseFloat(expression[0])));
            }
            else if(IsCharacter(expression)){
                var local = locals[expression];
                if(!local){
                    errors.push("Cant find local: "+expression);
                }
                else{
                    wasm.push(Opcode.get_local, ...unsignedLEB128(local.id));
                }
            }
            else{
                errors.push("Unexpected expression: "+expression);
            }
        }
        else{
            if(expression.length >= 3 && expression[0] == "+"){
                for(var i=1;i<expression.length;i++){
                    EmitExpression(expression[i]);
                    if(i>1){
                        wasm.push(Opcode.i32_add);
                    }
                }                
            }
            else{
                errors.push("Unexpected expression: "+expression);
            }
        }
    }

    for(var statement of f.body){
        if(statement[0] == "ret"){
            if(statement.length == 1){
                wasm.push(Opcode.return);
            }
            else if(statement.length == 2){
                EmitExpression(statement[1]);
                wasm.push(Opcode.return);
            }
            else{
                errors.push("ret expecting length 1 or 2. Has length: "+statement.length);
            }
        }
        else if(statement[0] == "var"){
            if(statement.length == 3){
                locals[statement[1]] = {id:localCount};
                EmitExpression(statement[2]);
                wasm.push(Opcode.set_local, ...unsignedLEB128(localCount));
                localCount++;
            }
            else{
                errors.push("var expecting length 3. Has length: "+statement.length);
            }
        }
        else{
            errors.push("Unexpected statement: "+JSON.stringify(statement));
        }
    }
    return encodeVector([...encodeVector([encodeLocal(localCount, Valtype.i32)]), ...wasm, Opcode.end])
}