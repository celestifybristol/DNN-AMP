
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;

namespace Risdall.Modules.DNN_AMP.Components
{
    public static class Extensions
    {

        /// <summary>
        /// Runs a Regex to remove all non-alphanumeric characters in a string
        /// </summary>
        /// <param name="s"></param>
        /// <returns>string</returns>
        public static string ToAlphaNumeric(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            try
            {
                return Regex.Replace(s, "[^a-zA-Z0-9]", "");
            }
            catch (System.Exception ex)
            {
                throw new System.Exception("StripToAlphaNumeric:", ex);
            }
        }
        public static int ToInt(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return 0;

            int.TryParse(s, out var i);
            return i;
        }
    }
}