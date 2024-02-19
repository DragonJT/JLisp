

static class Program{

    static void Main(){
        var code = @"
        (import void PrintInt (int i) ""console.log(i);"")

        (import void PrintChar (int c) ""console.log(GetChar(c));"")

        (import void PrintString (int i) ""console.log(GetString(i));"")

        (string Concat (string a string b)
            (var result (new string (+ (Length a) (Length b))))
            (for i 0 (Length a)
                (= (# result i) (# a i))
            )
            (for i 0 (Length b)
                (= (# result (+ i (Length a))) (# b i))
            )
            (ret result)
        )

        (export void Main ()
            (var a ""Hello "")
            (var b ""World "") 
            (var c ""--- Dont feed the chicken ---"")
            (var result (Concat (Concat a b) c))
            (= result (Concat result a))
            (PrintString result)

            (var x (- 10 5))
            (= x (+ x 3))
            (PrintInt x)
        )
        ";
        var il = SemanticAnalysis.Analyze(code);
        WasmEmitter.EmitAndRunWasm(il, "Main");
    }
} 