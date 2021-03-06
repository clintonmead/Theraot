﻿// Needed for NET40

using System;
using System.Diagnostics;

namespace Theraot.Core
{
    [DebuggerStepThrough]
    public static partial class NumericHelper
    {
        [CLSCompliant(false)]
        public static uint Abs(int a)
        {
            var mask = (uint)(a >> 31);
            return ((uint)a ^ mask) - mask;
        }

        public static int GCD(int left, int right)
        {
            if (left < 0)
            {
                left = -left;
            }

            if (right < 0)
            {
                right = -right;
            }

            return (int)GCD((uint)left, (uint)right);
        }

        public static long GCD(long left, long right)
        {
            if (left < 0)
            {
                left = -left;
            }

            if (right < 0)
            {
                right = -right;
            }

            return (long)GCD((ulong)left, (ulong)right);
        }

        [CLSCompliant(false)]
        public static uint GCD(uint left, uint right)
        {
            const int cvMax = 32;
            if (left < right)
            {
                Swap(ref left, ref right);
            }

            while (true)
            {
                if (right == 0)
                {
                    return left;
                }

                for (var cv = cvMax; ;)
                {
                    left -= right;
                    if (left < right)
                    {
                        break;
                    }

                    if (--cv != 0)
                    {
                        continue;
                    }

                    left %= right;
                    break;
                }

                Swap(ref left, ref right);
            }
        }

        [CLSCompliant(false)]
        public static ulong GCD(ulong uu1, ulong uu2)
        {
            const int cvMax = 32;
            if (uu1 < uu2)
            {
                Swap(ref uu1, ref uu2);
            }

            while (uu1 > uint.MaxValue)
            {
                if (uu2 == 0)
                {
                    return uu1;
                }

                for (var cv = cvMax; ;)
                {
                    uu1 -= uu2;
                    if (uu1 < uu2)
                    {
                        break;
                    }

                    if (--cv != 0)
                    {
                        continue;
                    }

                    uu1 %= uu2;
                    break;
                }

                Swap(ref uu1, ref uu2);
            }

            return GCD((uint)uu1, (uint)uu2);
        }

        [DebuggerNonUserCode]
        public static int Log2(int number)
        {
            if (number < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(number), "The logarithm of a negative number is imaginary.");
            }

            return Log2(unchecked((uint)number));
        }

        [DebuggerNonUserCode]
        public static int Log2(long number)
        {
            if (number < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(number), "The logarithm of a negative number is imaginary.");
            }

            return Log2(unchecked((ulong)number));
        }

        [CLSCompliant(false)]
        [DebuggerNonUserCode]
        public static int Log2(uint number)
        {
            if (number == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(number), "The logarithm of zero is not defined.");
            }

            number |= number >> 1;
            number |= number >> 2;
            number |= number >> 4;
            number |= number >> 8;
            number |= number >> 16;
            return PopulationCount(number >> 1);
        }

        [CLSCompliant(false)]
        [DebuggerNonUserCode]
        public static int Log2(ulong number)
        {
            if (number == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(number), "The logarithm of zero is not defined.");
            }

            number |= number >> 1;
            number |= number >> 2;
            number |= number >> 4;
            number |= number >> 8;
            number |= number >> 16;
            number |= number >> 32;
            return PopulationCount(number >> 1);
        }

        [DebuggerNonUserCode]
        public static int NextPowerOf2(int number)
        {
            if (number < 0)
            {
                return 1;
            }

            uint unsignedNumber;
            unchecked
            {
                unsignedNumber = (uint)number;
            }

            return (int)NextPowerOf2(unsignedNumber);
        }

        [CLSCompliant(false)]
        public static uint NextPowerOf2(uint number)
        {
            number |= number >> 1;
            number |= number >> 2;
            number |= number >> 4;
            number |= number >> 8;
            number |= number >> 16;
            return number + 1;
        }

        [DebuggerNonUserCode]
        public static int Sqrt(int number)
        {
            // Newton's  method  approximation  for  positive  integers
            // if  (number  ==  0)  return  0;
            var x = number >> 1;
            while (true)
            {
                var xNext = (x + (number / x)) >> 1;
                if (xNext >= x)
                {
                    return x;
                }

                x = xNext;
            }
        }

        public static void Swap<T>(ref T a, ref T b)
        {
            var tmp = a;
            a = b;
            b = tmp;
        }

        internal static uint GetHi(ulong uu)
        {
            return (uint)(uu >> 32);
        }

        internal static uint GetLo(ulong uu)
        {
            return (uint)uu;
        }
    }
}