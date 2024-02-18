
enum LispTokenType{Varname, Number, Object, String}
class LispToken(LispTokenType type, string value){
    public LispTokenType type = type;
    public string value = value;

    public string? GetName(){
        if(type == LispTokenType.Object){
            var index = 0;
            return ParseText(ref index);
        }
        return null;
    }

    public override string ToString()
    {
        string result = "";
        if(type == LispTokenType.Object){
            result+="(";
            var tokens = GetTokens();
            for(var i=0;i<tokens.Length;i++){
                result+=tokens[i].ToString();
                if(i<tokens.Length-1){
                    result+=" ";
                }
            }
            result+=")";
            return result;
        }
        else{
            return value;
        }
    }

    string ParseText(ref int index){
        string text = "";
        while(index<value.Length && !char.IsWhiteSpace(value[index])){
            text+=value[index];
            index++;
        }
        return text;
    }

    void ParseEnclosed(ref int index, char open, char close){
        var depth = 0;
        while(true){
            var c = value[index];
            index++;
            if(c == open){
                depth++;
            }
            else if(c == close){
                depth--;
                if(depth<0){
                    return;
                }
            }
        }
    }

    void ParseEnclosed(ref int index, char surrounds){
        while(true){
            var c = value[index];
            index++;
            if(c == surrounds){
                return;
            }
        }
    }

    public LispToken[] GetTokens(int index = 0){
        var tokens = new List<LispToken>();
        while(true){
            if(index>=value.Length){
                return [..tokens];
            }
            else if(value[index] == '('){
                index++;
                var start = index;
                ParseEnclosed(ref index, '(', ')');
                tokens.Add(new LispToken(LispTokenType.Object, value[start..(index-1)]));
            }
            else if(char.IsWhiteSpace(value[index])){
                index++;
            }
            else if(value[index] == '"'){
                index++;
                var start = index;
                ParseEnclosed(ref index, '"');
                tokens.Add(new LispToken(LispTokenType.String, value[start..(index-1)]));
            }
            else{
                var text = ParseText(ref index);
                if(char.IsDigit(text[0])){
                    tokens.Add(new LispToken(LispTokenType.Number, text));
                }
                else{
                    tokens.Add(new LispToken(LispTokenType.Varname, text));
                }
            }
        }
    }
}
