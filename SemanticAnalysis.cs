
static class SemanticAnalysis{        
    
    interface ILispNode{}

    class LispObject(ILispNode[] value):ILispNode{
        public ILispNode[] value = value;
        public int Length => value.Length;

        T Get<T>(int index) where T:ILispNode{
            if(index < value.Length){
                if(value[index] is T t){
                    return t;
                }
                throw new Exception($"{value[index]} is type {value[index].GetType()}, should be {typeof(T)}");
            }
            throw new Exception($"{index} >= {value.Length}: {ToString()}");
        }

        public LispObject GetObject(int index){
            return Get<LispObject>(index);
        }

        public string GetVarname(int index){
            return Get<LispVarname>(index).value;
        }

        public int GetInt(int index){
            return Get<LispInt>(index).value;
        }

        public string GetString(int index){
            return Get<LispString>(index).value;
        }

        public override string ToString(){
            var result = "(";
            for(var i=0;i<value.Length;i++){
                result+=value[i].ToString();
                if(i<value.Length-1){
                    result+=" ";
                }
            }
            return result+")";
        }
    }

    class LispString(string value):ILispNode{
        public string value = value;

        public override string ToString(){
            return '"'+value+'"';
        }
    }

    class LispVarname(string value):ILispNode{
        public string value = value;

        public override string ToString(){
            return value;
        }
    }

    class LispInt(int value):ILispNode{
        public int value = value;

        public override string ToString(){
            return value.ToString();
        }
    }

    class Parser(string code, int index){
        string code = code;
        int index = index;

        public LispObject Parse(){
            var result = new List<ILispNode>();
            while(true){
                if(index>=code.Length){
                    return new LispObject([..result]);
                }
                else if(code[index] == ')'){
                    index++;
                    return new LispObject([..result]);
                }
                else if(code[index] == '('){
                    index++;
                    result.Add(Parse());
                }
                else if(char.IsWhiteSpace(code[index])){
                    index++;
                }
                else if(code[index] == '"'){
                    index++;
                    var start = index;
                    while(true){
                        if(code[index] == '"'){
                            result.Add(new LispString(code[start..index]));
                            index++;
                            break;
                        }
                        index++;
                    }
                }
                else{
                    var start = index;
                    while(true){
                        bool breakVarname = index>=code.Length 
                            || code[index] == '('
                            || code[index] == ')' 
                            || code[index] == '"'  
                            || char.IsWhiteSpace(code[index]);
                        if(breakVarname){
                            var value = code[start..index];
                            if(char.IsDigit(value[0])){
                                result.Add(new LispInt(int.Parse(value)));
                            }
                            else{
                                result.Add(new LispVarname(value));
                            }
                            break;
                        }
                        index++;
                    }
                }
            }
        }
    }

    static ILInstruction[] AnalyzeOperator(LispObject expression, ILOpcode op){
        List<ILInstruction> result = [];
        for(var i=1;i<expression.Length;i++){
            result.AddRange(AnalyzeExpression(expression.value[i]));
            if(i>1){
                result.Add(new ILInstruction(op));
            }
        }
        return [..result];
    }

    static ILInstruction[] AnalyzeCall(LispObject call, string name){
        List<ILInstruction> result = [];
        for(var i=1;i<call.Length;i++){
            result.AddRange(AnalyzeExpression(call.value[i]));
        }
        result.Add(new ILInstruction(ILOpcode.Call, name));
        return [..result];
    }

    static ILInstruction[] AnalyzeExpression(ILispNode expression){
        if(expression is LispInt i){
            return [new ILInstruction(ILOpcode.I32Const, i.value)];
        }
        else if(expression is LispVarname v){
            return [new ILInstruction(ILOpcode.GetLocal, v.value)];
        }
        else if(expression is LispObject o){
            var name = o.GetVarname(0);
            if(name == "+"){
                return AnalyzeOperator(o, ILOpcode.I32Add);
            }
            else if(name == "-"){
                return AnalyzeOperator(o, ILOpcode.I32Sub);
            }
            else if(name == "*"){
                return AnalyzeOperator(o, ILOpcode.I32Mul);
            }
            else if(name == "/"){
                return AnalyzeOperator(o, ILOpcode.I32DivS);
            }
            return AnalyzeCall(o, name);
        }
        else if(expression is LispString s){
            return [new ILInstruction(ILOpcode.StringConst, s.value)];
        }
        throw new Exception("Unexpected type: "+expression.GetType());
    }

    static ILInstruction[] AnalyzeStatement(ILispNode statement){
        if(statement is LispObject objStatement){
            var name = objStatement.GetVarname(0);
            if(name == "ret"){
                if(objStatement.Length == 2){
                    return [..AnalyzeExpression(objStatement.value[1]), new ILInstruction(ILOpcode.Ret)];
                }
                else if(objStatement.Length == 1){
                    return [new ILInstruction(ILOpcode.Ret)];
                }
                else{
                    throw new Exception("Expecting ret 1 or 2 length: "+objStatement.ToString());
                }
            }
            else if(name == "var"){
                if(objStatement.Length == 3){
                    return [..AnalyzeExpression(objStatement.value[2]), 
                        new ILInstruction(ILOpcode.CreateLocal, objStatement.GetVarname(1))];
                }
                throw new Exception("Expecting var 3 length: "+objStatement.ToString());
            }
            else if(name == "++"){
                if(objStatement.Length == 2){
                    return [
                        new ILInstruction(ILOpcode.GetLocal, objStatement.GetVarname(1)),
                        new ILInstruction(ILOpcode.I32Const, 1),
                        new ILInstruction(ILOpcode.I32Add),
                        new ILInstruction(ILOpcode.SetLocal, objStatement.GetVarname(1))
                    ];
                }
                throw new Exception("++ Expecting 2 length: "+objStatement.ToString());
            }
            else{
                return AnalyzeCall(objStatement, name);                
            }
        }
        else{
            throw new Exception("Lisp Statement is not an object");
        }
    }

    static ILInstruction[] AnalyzeBody(ILispNode[] statements){
        var instructions = new List<ILInstruction>();
        for(var i=0;i<statements.Length;i++){
            instructions.AddRange(AnalyzeStatement(statements[i]));
        }
        return [..instructions];
    }

    static ILVariable[] AnalyzeParameters(LispObject parameters){
        var result = new List<ILVariable>();
        for(var i=0;i<parameters.Length;i+=2){
            var type = parameters.GetVarname(i);
            var name = parameters.GetVarname(i+1);
            result.Add(new ILVariable(type, name));
        }
        return [..result];
    }

     static ILFunction AnalyzeFunction(LispObject func, bool export){
        var ei = export?1:0;
        var returnType = func.GetVarname(0+ei);
        var name = func.GetVarname(1+ei);
        var parameters = AnalyzeParameters(func.GetObject(2+ei));
        var statements = func.value[(3+ei)..^0];
        var instructions = AnalyzeBody(statements);
        return new ILFunction(returnType, export, name, parameters, instructions);
    }

    public static IL Analyze(string lisp){
        var il = new IL();
        var tree = new Parser(lisp, 0).Parse();
        foreach(var node in tree.value){
            if(node is LispObject obj){
                var name = obj.GetVarname(0);
                if(name == "import"){
                    il.importFunctions.Add(new ILImportFunction(
                            obj.GetVarname(1), 
                            obj.GetVarname(2), 
                            AnalyzeParameters(obj.GetObject(3)), 
                            obj.GetString(4)));
                }
                else if(name == "export"){
                    il.functions.Add(AnalyzeFunction(obj, true));
                }
                else{
                    il.functions.Add(AnalyzeFunction(obj, false));
                }
            }
            else
            {
                throw new Exception("Expecting LispObject");
            }
        }
        return il;
    }
}