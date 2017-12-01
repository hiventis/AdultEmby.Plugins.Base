namespace AdultEmby.Plugins.Base
{
    public class StringUtils
    {
        /// <summary>
        /// The FuzzyStringCompare function may be used to compare two string for
        /// similarity.It is very useful in reducing "cascade" or
        /// "secondary" errors in compilers or other situations where
        /// symbol tables occur.
        /// </summary>
        /// <param name="first">The first string to consider.</param>
        /// <param name="second">The second string to consider.</param>
        /// <returns>
        /// double; 0 if the strings are entirly dissimilar, 1 if the
        /// strings are identical, and a number in between if they are
        /// similar.
        /// </returns>
        /// <remarks>
        /// Derived from PHP 5 similar_text() function
        ///
        /// The basic algorithm is described in:
        /// Oliver [1993] Programming Classics: Implementing the World's Best Algorithms by Oliver (ISBN 0-131-00413-1) 
        /// and the complexity is O(N**3) with N == length of longest string
        /// </remarks>
        public static double FuzzyStringCompare(string first, string second)
        {
            int len1 = first.Length;
            int len2 = second.Length;
            int score = 0;

            /* short-circuit obvious comparisons */
            if (len1 == 0 && len2 == 0)
                return 1.0F;
            if (len1 == 0 || len2 == 0)
                return 0.0F;

            score = SimilarText(first, second);
            /* The result is
            ((number of chars in common) / (average length of the strings)).
               This is admittedly biased towards finding that the strings are
               similar, however it does produce meaningful results.  */
            return ((double)score * 2.0 / (len1 + len2));
        }

        private static int SimilarText(string first, string second)
        {
            int sum = 0;
            int pos1 = 0;
            int pos2 = 0;
            int max = 0;
            int p, q, l;
            char[] arr1 = first.ToCharArray();
            char[] arr2 = second.ToCharArray();
            int firstLength = arr1.Length;
            int secondLength = arr2.Length;

            for (p = 0; p < firstLength; p++)
            {
                for (q = 0; q < secondLength; q++)
                {
                    for (l = 0; (p + l < firstLength) && (q + l < secondLength) && (arr1[p + l] == arr2[q + l]); l++) ;
                    if (l > max)
                    {
                        max = l;
                        pos1 = p;
                        pos2 = q;
                    }

                }
            }
            sum = max;
            if (sum > 0)
            {
                if (pos1 > 0 && pos2 > 0)
                {
                    sum += SimilarText(Slice(first, 0, pos1 > firstLength ? firstLength : pos1), Slice(second, 0, pos2 > secondLength ? secondLength : pos2));
                }

                if ((pos1 + max < firstLength) && (pos2 + max < secondLength))
                {
                    sum += SimilarText(Slice(first, pos1 + max, firstLength), Slice(second, pos2 + max, secondLength));
                }
            }
            return sum;
        }

        private static string Slice(string source, int start, int end)
        {
            if (end < 0) // Keep this for negative end support
            {
                end = source.Length + end;
            }
            int len = end - start;               // Calculate length
            return source.Substring(start, len); // Return Substring of length
        }
    }
}
