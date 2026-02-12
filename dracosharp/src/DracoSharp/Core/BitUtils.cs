using System.Runtime.CompilerServices;

namespace DracoSharp.Core;

public static class BitUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ConvertSymbolToSignedInt(uint val)
    {
        bool isPositive = (val & 1) == 0;
        val >>= 1;
        if (isPositive)
            return (int)val;
        return -(int)val - 1;
    }

    public static void ConvertSymbolsToSignedInts(ReadOnlySpan<uint> input, Span<int> output)
    {
        for (int i = 0; i < input.Length; i++)
            output[i] = ConvertSymbolToSignedInt(input[i]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MostSignificantBit(uint n)
    {
        if (n == 0) return -1;
        return 31 - int.LeadingZeroCount((int)n);
    }
}
