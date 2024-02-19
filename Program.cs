

static class Program{
    static void Main(){
        var code = @"
        (import void Print (int i) ""console.log(i);"")

        (import void PrintChar (int c) ""console.log(GetChar(c));"")

        (import void PrintString (int i) ""console.log(GetString(i));"")

        (export void Main ()
            (var first ""Hello"")
            (var second ""World"") 
            (for i 0 (Length first)
                (PrintChar (# first i))
            )
            (for i 0 (Length second)
                (PrintChar (# second i))
            )
        )
        ";
        var il = SemanticAnalysis.Analyze(code);
        WasmEmitter.EmitAndRunWasm(il, "Main");
    }
} 