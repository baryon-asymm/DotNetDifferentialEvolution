using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNetDifferentialEvolution.RandomProviders;

public class RandomProvider : Random
{
    public RandomProvider()
    {
    }

    public RandomProvider(
        int Seed) : base(Seed)
    {
    }
    
    // Algorithm link https://en.wikipedia.org/wiki/Xorshift#xorshift.2A
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector<double> NextVectorByXorShift64(Vector<double> vector)
    {
        const double doubleToUlongConvert = ulong.MaxValue;
        const double ulongToDoubleConvert = 1.0 / doubleToUlongConvert;
        
        var ulongVector = Vector.AsVectorUInt64(vector);
        
        //ulongVector ^= ulongVector >> 12;
        //ulongVector ^= ulongVector << 25;
        //ulongVector ^= ulongVector >> 27;
        //ulongVector *= 0x2545F4914F6CDD1DUL;
        //var a = NextDouble();
        
        return Vector.AsVectorDouble(ulongVector);
    }
}
