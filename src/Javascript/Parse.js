function LispParser(code){
    var index = 0;

    function IsDigit(c){
        return c>='0' && c<='9';
    }

    function IsWhitespace(c){
        return c==' ' || c=='\t' || c=='\n' || c=='\r';
    }

    function Parse(){
        var result = ['obj'];
        while(true){
            if(index>=code.length){
                return result;
            }
            else if(code[index] == ')'){
                index++;
                return result;
            }
            else if(code[index] == '('){
                index++;
                result.push(Parse());
            }
            else if(IsWhitespace(code[index])){
                index++;
            }
            else if(code[index] == '"'){
                index++;
                var start = index;
                while(true){
                    if(code[index] == '"'){
                        result.push(['string', code.substring(start, index)]);
                        index++;
                        break;
                    }
                    index++;
                }
            }
            else{
                var start = index;
                while(true){
                    var breakVarname = index>=code.length
                        || code[index] == '('
                        || code[index] == ')'
                        || code[index] == '"'
                        || IsWhitespace(code[index]);
                    if(breakVarname){
                        var value = code.substring(start, index);
                        if(IsDigit(value[0])){
                            result.push(['int', parseFloat(value)]);
                        }
                        else{
                            result.push(['varname', value]);
                        }
                        break;
                    }
                    index++;
                }
            }
        }
    }

    return Parse();
}

class Literal{
    constructor(literal){
        this.literal = literal;
    }

    Check(obj){
        return (obj[0] == 'varname' && obj[1] == this.literal);
    }

    Parse(obj){
        return undefined;
    }
}

class String{
    Check(obj){
        return (obj[0] == 'string');
    }

    Parse(obj){
        return obj[1];
    }
}

class Varname{
    Check(obj){
        return (obj[0] == 'varname');
    }

    Parse(obj){
        return obj[1];
    }
}

class Int{
    Check(obj){
        return (obj[0] == 'int');
    }

    Parse(obj){
        return obj[1];
    }
}

class Or{
    constructor(branches){
        this.Init(branches);
    }

    Init(branches){
        this.branches = branches;
    }

    Check(obj){
        for(var b of this.branches){
            if(b.Check(obj)){
                return true;
            }
        }
        return false;
    }

    Parse(obj){
        for(var b of this.branches){
            if(b.Check(obj)){
                var result = b.Parse(obj);
                if(typeof result != 'object'){
                    return {type:b.constructor.name, value:result};
                }
                return result;
            }
        }
        errors.push("not found branch: "+JSON.stringify(this.branches.map(o=>o.type))+" got: "+JSON.stringify(obj));
    }
}

class ArrayMultipleOf2{
    constructor(item1, item2){
        this.IsArrayMultipleOf2 = true;
        this.Init(item1, item2);
    }

    Init(item1, item2){
        this.item1 = item1;
        this.item2 = item2;
    }

    Check(obj){
        return (obj[0] == 'obj');
    }

    Parse(obj){
        if((obj.length-1)%2 == 0){
            var result = [];
            for(var i=1;i<obj.length;i+=2){
                var resultObj = {};
                resultObj[this.item1[0]] = this.item1[1].Parse(obj[i]);
                resultObj[this.item2[0]] = this.item2[1].Parse(obj[i+1]);
                result.push(resultObj);
            }
            return result;
        }
        else{
            errors.push('Expecting obj length to be multiple of 2: '+JSON.stringify(obj));
        }
    }
}

class Params{
    constructor(name, element, min, max){
        this.name = name;
        this.element = element;
        this.min = min;
        this.max = max;
    }
}

class Obj{
    constructor(type, fields, params){
        this.Init(type, fields, params);
    }

    Init(type, fields, params){
        this.type = type;
        this.fields = fields;
        this.params = params;
    }

    CheckLength(obj){
        if(this.params){
            var minValid = this.params.min==undefined || (obj.length >= this.fields.length+1+this.params.min);
            var maxValid = this.params.max==undefined || (obj.length <= this.fields.length+1+this.params.max);
            return minValid && maxValid;
        }
        else{
            return obj.length == this.fields.length+1;
        }
    }

    Check(obj){
        if(obj[0] != 'obj'){
            return false;
        }
        if(!this.CheckLength(obj)){
            return false;
        }
        for(var i=0;i<this.fields.length;i++){
            if(!Array.isArray(this.fields[i])){
                if(!this.fields[i].Check(obj[i+1])){
                    return false;
                }
            }
            else{
                if(!this.fields[i][1].Check(obj[i+1])){
                    return false;
                }
            }
        }
        if(this.params){
            for(var i=this.fields.length+1;i<obj.length;i++){
                if(!this.params.element.Check(obj[i])){
                    return false;
                }
            }
        }
        return true;
    }

    Parse(obj){
        var result = {};
        result.type = this.type;
        for(var i=0;i<this.fields.length;i++){
            if(Array.isArray(this.fields[i])){
                var name = this.fields[i][0];
                var parser = this.fields[i][1];
                result[name] = parser.Parse(obj[i+1]);
            }
        }
        if(this.params){
            var name = this.params.name;
            var paramsResult = [];
            for(var i=this.fields.length+1;i<obj.length;i++){
                paramsResult.push(this.params.element.Parse(obj[i]));
            }
            result[name] = paramsResult;
        }
        return result;
    }
}

