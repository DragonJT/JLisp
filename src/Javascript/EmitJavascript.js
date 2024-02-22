function EmitJavascript(tree){

    function EmitOperator(expression, op){
        return '('+EmitExpression(expression.values[0])+op+EmitExpression(expression.values[1])+')';
    }

    function EmitArithmeticOperator(expression, op){
        var result = '('
        for(var i=0;i<expression.values.length;i++){
            result+=EmitExpression(expression.values[i]);
            if(i<expression.values.length-1){
                result+=op;
            }
        }
        return result+')';
    }

    function EmitCall(call){
        var result = call.name+'(';
        for(var i=0;i<call.args.length;i++){
            result+=EmitExpression(call.args[i]);
            if(i<call.args.length-1){
                result+=',';
            }
        }
        return result+')';
    }

    function EmitObj(expression){
        var result = '{';
        for(var i=0;i<expression.values.length;i++){
            var field = expression.values[i];
            result+=field.name+':'+EmitExpression(field.value);
            if(i<expression.values.length-1){
                result+=',';
            }
        }
        return result+'}';
    }

    function EmitExpression(expression){
        if(expression.type == 'Number'){
            return expression.value;
        }
        else if(expression.type == 'Varname'){
            return expression.value;
        }
        else if(expression.type == 'String'){
            return '"'+expression.value+'"';
        }
        else if(expression.type == 'call'){
            return EmitCall(expression);
        }
        else if(expression.type == 'obj'){
            return EmitObj(expression);
        }
        else if(expression.type == '+'){
            return EmitArithmeticOperator(expression, '+');
        }
        else if(expression.type == '-'){
            return EmitArithmeticOperator(expression, '-');
        }
        else if(expression.type == '*'){
            return EmitArithmeticOperator(expression, '*');
        }
        else if(expression.type == '/'){
            return EmitArithmeticOperator(expression, '/');
        }
        else if(expression.type == '<'){
            return EmitOperator(expression, '<');
        }
        else if(expression.type == '>'){
            return EmitOperator(expression, '>');
        }
        else{
            throw 'Unexpected expression: '+JSON.stringify(expression);
        }
    }

    function EmitParameters(parameters){
        var result = '(';
        for(var i=0;i<parameters.values.length;i++){
            result+=parameters.values[i];
            if(i<parameters.values.length-1){
                result+=',';
            }
        }
        return result+')';
    }

    function EmitStatement(statement){
        if(statement.type == 'fn'){
            return 'function '+statement.name+EmitParameters(statement.parameters)+'{\n'+ EmitBody(statement.body)+'}\n';
        }
        else if(statement.type == 'return'){
            var result='return';
            if(statement.values.length == 1){
                result+= ' '+EmitExpression(statement.values[0]);
            }
            result+=';\n';
            return result;
        }
        else if(statement.type == 'var'){
            return 'var '+statement.name+' = '+EmitExpression(statement.value)+';\n';
        }
        else if(statement.type == 'call'){
            return EmitCall(statement)+';\n';
        }
        else if(statement.type == 'if'){
            return 'if('+EmitExpression(statement.condition)+'){\n'+EmitBody(statement.body)+'}\n';
        }
        else{
            throw 'Unexpected statement: '+JSON.stringify(statement);
        }
    }

    function EmitBody(body){
        var result = '';
        for(var s of body){
            result+=EmitStatement(s);
        }
        return result;
    }

    return EmitBody(tree.values);
}