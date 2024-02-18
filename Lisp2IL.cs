
static class Lisp2IL{        

    static string CompileOperator(LispToken expression, string name, string op){
        var tokens = expression.GetTokens(name.Length);
        string result = "";
        for(var i=0;i<tokens.Length;i++){
            result+=CompileExpression(tokens[i]);
            if(i>0){
                result+=op;
            }
        }
        return result;
    }

    static string CompileCall(LispToken token, string name){
        var tokens = token.GetTokens(name.Length);
        string result = "";
        for(var i=0;i<tokens.Length;i++){
            result+=CompileExpression(tokens[i]);
        }
        result+="(call "+name+")";
        return result;
    }

    static string CompileExpression(LispToken expression){
        if(expression.type == LispTokenType.Number){
            return "(i32.const "+expression.value+")";
        }
        else if(expression.type == LispTokenType.Varname){
            return "(get_local "+expression.value+")";
        }
        else if(expression.type == LispTokenType.Object){
            var name = expression.GetName();
            if(name == "+"){
                return CompileOperator(expression, name, "(i32.add)");
            }
            else if(name == "-"){
                return CompileOperator(expression, name, "(i32.sub)");
            }
            else if(name == "*"){
                return CompileOperator(expression, name, "(i32.mul)");
            }
            else if(name == "/"){
                return CompileOperator(expression, name, "(i32.div_s)");
            }
            else if(name!=null){
                return CompileCall(expression, name);
            }
            else{
                throw new Exception("name is null");
            }
        }
        else if(expression.type == LispTokenType.String){
            return "(string.const \""+expression.value+"\")";
        }
        else{
            return "(error \"Unexpected token type"+expression.type.ToString()+"\")";
        }
    }

    static string CompileStatement(LispToken statement){
        if(statement.type == LispTokenType.Object){
            var name = statement.GetName();
            if(name == null){
                return "(error \"name is null: "+statement.ToString()+"\")";
            }
            else if(name == "ret"){
                var tokens = statement.GetTokens(name.Length);
                if(tokens.Length == 1){
                    return CompileExpression(tokens[0])+"(ret)";
                }
                else if(tokens.Length == 0){
                    return "(ret)";
                }
                else{
                    return "(error \"ret expecting 0 or 1 tokens\")";
                }
            }
            else if(name == "var"){
                var tokens = statement.GetTokens(name.Length);
                if(tokens.Length == 2){
                    if(tokens[0].type == LispTokenType.Varname){
                        return CompileExpression(tokens[1]) + "(create_local "+tokens[0].value+")";
                    }
                    return "(error \"varname token is not varname: "+tokens[0].value+"\")";
                }
                return "(error \"Unexpected tokens length: "+tokens.Length+"\")";
            }
            else if(name == "++"){
                var tokens = statement.GetTokens(name.Length);
                if(tokens.Length == 1 && tokens[0].type == LispTokenType.Varname){
                    return $"(get_local {tokens[0].value})(i32.const 1)(i32.add)(set_local {tokens[0].value})";
                }
                return "(error \"++ expecting 1 token of type varname)";
            }
            else{
                return CompileCall(statement, name);                
            }
        }
        else{
            return "(error \"Statement is not object\")";
        }
    }

    static string CompileBody(LispToken[] statements){
        var result = "";
        foreach(var statement in statements){
            result+=CompileStatement(statement);
        }
        return result;
    }

     static string CompileFunction(LispToken func){
        var tokens = func.GetTokens();
        var export = tokens[0].value == "export";
        var ei = export?1:0;
        var returnType = tokens[0+ei];
        var name = tokens[1+ei];
        if(name.type != LispTokenType.Varname){
            return "(error \"function name should be varname: "+name+"\")";
        }
        var parameters = tokens[2+ei];
        var statements = tokens[(3+ei)..^0];
        string instructions = CompileBody(statements);
        return "(fn "+(export?"t":"f")+" "+returnType+" "+name+" "+parameters+"("+instructions+"))";
    }

    public static string Compile(string lisp){
        var tokens = new LispToken(LispTokenType.Object, lisp).GetTokens();
        string result = "";
        foreach(var t in tokens){
            var name = t.GetName();
            if(name == null){
                result+="(error \"name is null: "+t+"\")";
            }
            else if(name == "import"){
                result+="("+t.value+")";
            }
            else{
                result+=CompileFunction(t);
            }
        }
        return result;
    }
}