using System;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;

namespace AdultEmby.Plugins.Base
{
    public class HtmlExtractorUtils
    {
        public static int? ToInt(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                int intValue;
                if (int.TryParse(value, out intValue))
                {
                    return intValue;
                }
            }
            return null;
        }

        public static string Text(IElement element)
        {
            string text = null;
            try
            {
                if (element != null)
                {
                    text = Trim(element.Text());
                }
            }
            catch (Exception)
            {

            }
            return text;
        }

        public static string StripPrefix(string prefix, string value)
        {
            string strippedValue = null;
            try
            {
                if (value != null)
                {
                    if (!string.IsNullOrEmpty(prefix) && value.StartsWith(prefix))
                    {
                        var regex = new Regex(Regex.Escape(prefix));
                        strippedValue = regex.Replace(value, "", 1);
                    }
                    else
                    {
                        strippedValue = value;
                    }
                }
            }
            catch (Exception)
            {

            }
            return strippedValue;
        }

        public static string Trim(string value)
        {
            string trimmedValue = null;
            try
            {
                if (value != null)
                {
                    trimmedValue = value.Trim();
                }
            }
            catch (Exception)
            {

            }
            return trimmedValue;
        }

        public static string AttributeValue(IElement element, string attributeName)
        {
            string value = null;
            if (element.HasAttribute(attributeName))
            {
                value = element.Attributes[attributeName].Value;
            }
            return Trim(value);
        }

        public static string Part(string path, char separator, int index)
        {
            string part = null;
            if (path != null)
            {
                string[] parts = path.Split(separator);
                if (index < parts.Length)
                {
                    part = parts[index];
                }
            }
            return part;
        }

        public static bool HasSuffix(string value, string suffix)
        {
            return value != null && suffix != null && value.EndsWith(suffix);
        }

        public static DateTime? ToDateTime(string value, string format)
        {
            DateTime? dateTime = null;
            if (value != null)
            {
                try
                {
                    dateTime = DateTime.ParseExact(value, format,
                        System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                }
            }
            return dateTime;
        }

        public static string ImageSource(IHtmlImageElement element)
        {
            string url = null;
            if (element != null)
            {
                url = Trim(element.Source);
            }
            return url;
        }
        //        public DateTime toDateTime(string value, string format)
        //        {
        //            DateTime dateTime = null;
        //            if (DateTime.TryParseExact(value, format,
        //                            new CultureInfo("en-US"), DateTimeStyles.None, out dateTime))
        //            {
        //                //item.PremiereDate = date.ToUniversalTime();
        //            }
        //        }

    public static IElement FindElementContainingText(IHtmlCollection<IElement> elements, string text)
    {
        IElement result = null;
        foreach (var element in elements)
        {
            string elementText = Text(element);
            if (!string.IsNullOrEmpty(elementText) && elementText.Contains(text))
            {
                result = element;
                break;
            }
        }
        return result;
    }
    }
}
