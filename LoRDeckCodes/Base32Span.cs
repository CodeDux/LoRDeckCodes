using System;

namespace LoRDeckCodes
{
    /*
 * Derived from https://github.com/google/google-authenticator-android/blob/master/AuthenticatorApp/src/main/java/com/google/android/apps/authenticator/Base32String.java
 * 
 * Copyright (C) 2016 BravoTango86
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;

    public static class Base32Span
    {

        private static readonly char[] DIGITS;
        private static readonly int MASK;
        private static readonly int SHIFT;
        private static Dictionary<char, int> CHAR_MAP = new Dictionary<char, int>();
        private const string SEPARATORS = "-=";

        static Base32Span()
        {
            DIGITS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();
            MASK = DIGITS.Length - 1;
            SHIFT = NumberOfTrailingZeros(DIGITS.Length);
            for (int i = 0; i < DIGITS.Length; i++) CHAR_MAP[DIGITS[i]] = i;
        }

        private static int NumberOfTrailingZeros(int i)
        {
            // HD, Figure 5-14
            int y;
            if (i == 0) return 32;
            int n = 31;
            y = i << 16; if (y != 0) { n = n - 16; i = y; }
            y = i << 8; if (y != 0) { n = n - 8; i = y; }
            y = i << 4; if (y != 0) { n = n - 4; i = y; }
            y = i << 2; if (y != 0) { n = n - 2; i = y; }
            return n - (int)((uint)(i << 1) >> 31);
        }

        public static int Prepare(in ReadOnlySpan<char> encoded)
        {
            var length = 0;
            foreach (var c in encoded)
            {
                if (SEPARATORS.Contains(c))
                    continue;

                length++;
            }

            if (length == 0)
            {
                return 0;
            }

            return length * SHIFT / 8;
        }

        public static void Decode(in ReadOnlySpan<char> encoded, ref Span<byte> result)
        {
            int buffer = 0;
            int next = 0;
            int bitsLeft = 0;
            foreach (var c in encoded)
            {
                if (!CHAR_MAP.TryGetValue(char.ToUpper(c), out var value))
                {
                    if (SEPARATORS.Contains(c))
                        continue;
                    throw new DecodingException("Illegal character: " + c);
                }
                buffer <<= SHIFT;
                buffer |= value & MASK;
                bitsLeft += SHIFT;
                if (bitsLeft >= 8)
                {
                    result[next++] = (byte)(buffer >> (bitsLeft - 8));
                    bitsLeft -= 8;
                }
            }
            // We'll ignore leftover bits for now.
            //
            // if (next != outLength || bitsLeft >= SHIFT) {
            //  throw new DecodingException("Bits left: " + bitsLeft);
            // }
        }


        public static string Encode(byte[] data, bool padOutput = false)
        {
            if (data.Length == 0)
            {
                return "";
            }

            // SHIFT is the number of bits per output character, so the length of the
            // output is the length of the input multiplied by 8/SHIFT, rounded up.
            if (data.Length >= (1 << 28))
            {
                // The computation below will fail, so don't do it.
                throw new ArgumentOutOfRangeException("data");
            }

            int outputLength = (data.Length * 8 + SHIFT - 1) / SHIFT;
            StringBuilder result = new StringBuilder(outputLength);

            int buffer = data[0];
            int next = 1;
            int bitsLeft = 8;
            while (bitsLeft > 0 || next < data.Length)
            {
                if (bitsLeft < SHIFT)
                {
                    if (next < data.Length)
                    {
                        buffer <<= 8;
                        buffer |= (data[next++] & 0xff);
                        bitsLeft += 8;
                    }
                    else
                    {
                        int pad = SHIFT - bitsLeft;
                        buffer <<= pad;
                        bitsLeft += pad;
                    }
                }
                int index = MASK & (buffer >> (bitsLeft - SHIFT));
                bitsLeft -= SHIFT;
                result.Append(DIGITS[index]);
            }
            if (padOutput)
            {
                int padding = 8 - (result.Length % 8);
                if (padding > 0) result.Append(new string('=', padding == 8 ? 0 : padding));
            }
            return result.ToString();
        }

        public static string EncodeNoAlloc(byte[] data, bool padOutput = false)
        {
            if (data.Length == 0)
            {
                return "";
            }

            // SHIFT is the number of bits per output character, so the length of the
            // output is the length of the input multiplied by 8/SHIFT, rounded up.
            if (data.Length >= (1 << 28))
            {
                // The computation below will fail, so don't do it.
                throw new ArgumentOutOfRangeException(nameof(data));
            }

            int outputLength = (data.Length * 8 + SHIFT - 1) / SHIFT;
            Span<char> result = stackalloc char[outputLength];
            int resultIndex = 0;

            int buffer = data[0];
            int next = 1;
            int bitsLeft = 8;
            while (bitsLeft > 0 || next < data.Length)
            {
                if (bitsLeft < SHIFT)
                {
                    if (next < data.Length)
                    {
                        buffer <<= 8;
                        buffer |= (data[next++] & 0xff);
                        bitsLeft += 8;
                    }
                    else
                    {
                        int pad = SHIFT - bitsLeft;
                        buffer <<= pad;
                        bitsLeft += pad;
                    }
                }
                int index = MASK & (buffer >> (bitsLeft - SHIFT));
                bitsLeft -= SHIFT;
                result[resultIndex++] = DIGITS[index];
            }
//            if (padOutput)
//            {
//                int padding = 8 - (result.Length % 8);
//                if (padding > 0) result.Append(new string('=', padding == 8 ? 0 : padding));
//            }
            return result.ToString();
        }

        private class DecodingException : Exception
        {
            public DecodingException(string message) : base(message)
            {
            }
        }
    }
}
