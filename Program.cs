

static class Program{
    static void Main(){
        var code = @"
        (import void Print (int i) ""console.log(i);"")

        (import void PrintString (int i) ""console.log(GetString(i));"")

        (int Test () 
            (ret ""woohoo!"")
        )

        (int GetNumber ()
            (ret (+ 4 2))
        )

        (export void Main ()
            (var x 5)
            (++ x)
            (Print x)
            (PrintString ""hello world"")
            (PrintString ""BAAAAAAA"")
            (PrintString (Test))
            (Print (GetNumber))
        )
        ";
        var il = Lisp2IL.Compile(code);
        Console.WriteLine(il);
        WasmEmitter.EmitAndRunWasm(il, "Main");
    }
}