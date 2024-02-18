enum Opcode
{
    block = 0x02,
    loop = 0x03,
    br = 0x0c,
    br_if = 0x0d,
    end = 0x0b,
    ret = 0x0f,
    call = 0x10,
    drop = 0x1a,
    get_local = 0x20,
    set_local = 0x21,
    i32_store_8 = 0x3a,
    i32_store = 0x36,
    i32_const = 0x41,
    f32_const = 0x43,
    i32_eqz = 0x45,
    i32_eq = 0x46,
    f32_eq = 0x5b,
    f32_lt = 0x5d,
    f32_gt = 0x5e,
    i32_and = 0x71,
    f32_add = 0x92,
    f32_sub = 0x93,
    f32_mul = 0x94,
    f32_div = 0x95,
    f32_neg = 0x8c,
    i32_trunc_f32_s = 0xa8,
    f32_load = 0x2a,
    f32_store = 0x38,
    i32_mul = 0x6c,
    i32_add = 0x6a,
    i32_sub = 0x6b,
    i32_div_s = 0x6d,
    f32_convert_i32_s = 0xb2,
    i32_lt_s = 0x48,
    i32_gt_s = 0x4a,
    @if = 0x04,
}

enum SectionType
{
    Custom = 0,
    Type = 1,
    Import = 2,
    Func = 3,
    Table = 4,
    Memory = 5,
    Global = 6,
    Export = 7,
    Start = 8,
    Element = 9,
    Code = 10,
    Data = 11
}

enum Valtype
{
    Void = -1,
    I32 = 0x7f,
    F32 = 0x7d
}

enum ExportType
{
    Func = 0x00,
    Table = 0x01,
    Mem = 0x02,
    Global = 0x03
}

// https://webassembly.github.io/spec/core/binary/types.html#binary-blocktype
// https://github.com/WebAssembly/design/blob/main/BinaryEncoding.md#value_type
enum Blocktype
{
    @void = 0x40,
    i32 = 0x7f,
}

static class WasmHelper
{
    public const byte emptyArray = 0x0;
    public const byte functionType = 0x60;

    public static byte[] MagicModuleHeader => [0x00, 0x61, 0x73, 0x6d];

    public static byte[] ModuleVersion => [0x01, 0x00, 0x00, 0x00];

    public static byte[] MemoryImport =>[
        ..String("env"),
        ..String("memory"),
        (byte)ExportType.Mem,
        /* limits https://webassembly.github.io/spec/core/binary/types.html#limits -
          indicates a min memory size of one page */
        0x00,
        ..UnsignedLEB128(10),
      ];

    public static byte[] Ieee754(float value)
    {
        return BitConverter.GetBytes(value);
    }

    public static byte[] SignedLEB128(int value)
    {
        List<byte> bytes = [];
        bool more = true;

        while (more)
        {
            byte chunk = (byte)(value & 0x7fL); // extract a 7-bit chunk
            value >>= 7;

            bool signBitSet = (chunk & 0x40) != 0; // sign bit is the msb of a 7-bit byte, so 0x40
            more = !((value == 0 && !signBitSet) || (value == -1 && signBitSet));
            if (more) { chunk |= 0x80; } // set msb marker that more bytes are coming

            bytes.Add(chunk);
        }
        return bytes.ToArray();
    }

    public static byte[] UnsignedLEB128(uint value)
    {
        List<byte> bytes = [];
        do
        {
            byte byteValue = (byte)(value & 0x7F); // Extract 7 bits
            value >>= 7; // Shift right by 7 bits

            if (value != 0)
                byteValue |= 0x80; // Set the high bit to indicate more bytes

            bytes.Add(byteValue);
        }
        while (value != 0);
        return bytes.ToArray();
    }

    public static byte[] String(string value)
    {
        List<byte> bytes = [.. UnsignedLEB128((uint)value.Length)];
        foreach (var v in value)
        {
            bytes.Add((byte)v);
        }
        return bytes.ToArray();
    }

    public static byte[] Vector(params byte[][] vector)
    {
        return [..UnsignedLEB128((uint)vector.Length), ..vector.SelectMany(b=>b).ToArray()];
    }

    public static byte[] Vector(params byte[] vector)
    {
        return [..UnsignedLEB128((uint)vector.Length), ..vector];
    }

    public static byte[] Local(uint count, Valtype valtype)
    {
        return [..UnsignedLEB128(count), (byte)valtype];
    }

    public static byte[] Section(SectionType section, byte[][] bytes)
    {
        return [(byte)section, .. Vector(Vector(bytes))];
    }

    public static byte[] Return(Valtype type)
    {
        if (type == Valtype.Void)
        {
            return [emptyArray];
        }
        else
        {
            return Vector((byte)type);
        }
    }
}