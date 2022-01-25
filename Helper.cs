using System;


public static class Helper
{
    public static string[] Split(this string line, char tempSeparator, string separator, StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
        => line.Replace(separator, new string(tempSeparator, 1)).Split(new char[] { tempSeparator }, options);
    public static string[] Split(this string line, char tempSeparator, string[] separators, StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
    {
        for (int i = 0; i < separators.Length; i++)
            line.Replace(separators[i], new string(tempSeparator, 1));

        return line.Split(new char[] { tempSeparator }, options);
    }
}