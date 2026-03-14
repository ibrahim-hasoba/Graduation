using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Utilities
{
    public static class BusinessCodeGenerator
    {
        public const string UserPrefix = "U";
        public const string ProductPrefix = "P";
        public const string VendorPrefix = "V";
        public const string CategoryPrefix = "C";
        public const string OrderPrefix = "ORD";

        public static string ForUser(string guidId)
            => Build(UserPrefix, guidId);

        public static string ForProduct(int id)
            => Build(ProductPrefix, id.ToString());

        public static string ForVendor(int id)
            => Build(VendorPrefix, id.ToString());

        public static string ForCategory(int id)
            => Build(CategoryPrefix, id.ToString());


        private static string Build(string prefix, string rawId)
        {
            var suffix = ComputeSuffix(rawId);
            return $"{prefix}-{suffix}";
        }
        private static string ComputeSuffix(string input)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hash = System.Security.Cryptography.MD5.HashData(bytes);
            return Convert.ToHexString(hash)[..6];
        }
    }
}
