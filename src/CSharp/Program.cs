
class Program{

    static string UpdateFile(string javascriptFolder, string file){
        var lines = File.ReadAllLines(javascriptFolder+file);
        var code = "";
        foreach(var l in lines){
            if(l.StartsWith("//#")){
                code += UpdateFile(javascriptFolder, l[3..]);
            }
            else{
                code+=l+'\n';
            }
        }
        return code;
    }

    static void Main(){
        string lispProgram = File.ReadAllText("src/JLisp/Main.jlisp");
        var javascriptFolder = "src/Javascript/";
        string[] javascriptFiles = ["Main.js"];
        string javascript = "";
        
        foreach(var f in javascriptFiles){
            javascript+=UpdateFile(javascriptFolder, f);
        }

        string html = @"
<!DOCTYPE html>
<html>
<head>
  <title>JLisp</title>
</head>
<body>
  <script>
  var lispProgram = `"+lispProgram+@"`;
  "+javascript+@"
  </script>
</body>
</html>";
    File.WriteAllText("index.html", html);
    }
}