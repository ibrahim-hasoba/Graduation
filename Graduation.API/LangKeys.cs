namespace Graduation.API
{
    public static class LangKeys
    {
        public const string NotAuthenticated = "NotAuthenticated";

        public static class Auth
        {
            public const string AccountNotFound = "Account_NotFound";
            public const string AccountLocked = "Account_Locked";
            public const string AccountDeleted = "Account_Deleted";
            public const string InvalidCredentials = "Invalid_Credentials";
            public const string EmailNotVerified = "Email_NotVerified";
            public const string EmailAlreadyExists = "Email_AlreadyExists";
            public const string EmailAlreadyVerified = "Email_AlreadyVerified";
            public const string EmailConfirmFailed = "Email_ConfirmFailed";
            public const string PhoneAlreadyRegistered = "Phone_AlreadyRegistered";
            public const string RegistrationSuccess = "Registration_Success";
            public const string RegistrationEmailFailed = "Registration_EmailFailed";
            public const string NotAdmin = "NotAdmin";
            public const string LogoutSuccess = "Logout_Success";
            public const string SessionInvalid = "Session_Invalid";
            public const string UserNoLongerExists = "User_NoLongerExists";
            public const string TokenRefreshFailed = "Token_RefreshFailed";
            public const string LoginGoogleInvalidToken = "Login_GoogleInvalidToken";
            public const string LoginGoogleFailed = "Login_GoogleFailed";
            public const string FcmEmpty = "FCM_Empty";
            public const string FcmUpdated = "FCM_Updated";
        }

        public static class Otp
        {
            public const string RecentlySent = "OTP_RecentlySent";
            public const string TooMany = "OTP_TooMany";
            public const string Expired = "OTP_Expired";
            public const string CodeRecentlySent = "Code_RecentlySent";
            public const string CodeTooMany = "Code_TooMany";
        }

        public static class Password
        {
            public const string Invalid = "Password_Invalid";
            public const string CodeInvalid = "Password_CodeInvalid";
            public const string ResetSuccess = "Password_ResetSuccess";
            public const string ChangeSuccess = "Password_ChangeSuccess";
            public const string ForgotPasswordSent = "ForgotPassword_Sent";
            public const string ForgotPasswordCodeSent = "ForgotPassword_CodeSent";
            public const string ResetCodeValid = "ResetCode_Valid";
        }

        public static class Verification
        {
            public const string Sent = "Verification_Sent";
            public const string NewSent = "Verification_NewSent";
        }

        public static class Profile
        {
            public const string UpdateSuccess = "Profile_UpdateSuccess";
            public const string PictureUpdateSuccess = "ProfilePicture_UpdateSuccess";
            public const string PictureDeleteSuccess = "ProfilePicture_DeleteSuccess";
        }

        public static class Address
        {
            public const string MaxReached = "Address_MaxReached";
            public const string Added = "Address_Added";
            public const string NotFound = "Address_NotFound";
            public const string Updated = "Address_Updated";
            public const string AlreadyDefault = "Address_AlreadyDefault";
            public const string DefaultUpdated = "Address_DefaultUpdated";
            public const string Deleted = "Address_Deleted";
        }

        public static class Cart
        {
            public const string ItemAdded = "Cart_ItemAdded";
            public const string ItemUpdated = "Cart_ItemUpdated";
            public const string ItemRemoved = "Cart_ItemRemoved";
            public const string Cleared = "Cart_Cleared";
        }

        public static class Category
        {
            public const string NotFound = "Category_NotFound";
            public const string Created = "Category_Created";
            public const string Updated = "Category_Updated";
            public const string Activated = "Category_Activated";
            public const string Deactivated = "Category_Deactivated";
            public const string Deleted = "Category_Deleted";
            public const string LeafCategories = "Category_LeafCategories";
        }

        public static class Image
        {
            public const string NoFile = "Image_NoFile";
            public const string NoFiles = "Image_NoFiles";
            public const string NotFound = "Image_NotFound";
            public const string Uploaded = "Image_Uploaded";
            public const string MultipleUploaded = "Image_MultipleUploaded";
            public const string ProductUploaded = "Image_ProductUploaded";
            public const string LogoUploaded = "Image_LogoUploaded";
            public const string BannerUploaded = "Image_BannerUploaded";
            public const string Deleted = "Image_Deleted";
            public const string MaxExceeded = "Image_MaxExceeded";
            public const string UrlRequired = "Image_UrlRequired";
        }

        public static class Order
        {
            public const string Placed = "Order_Placed";
            public const string NotVendor = "Order_NotVendor";
            public const string UpdateNotVendor = "Order_UpdateNotVendor";
            public const string StatusUpdated = "Order_StatusUpdated";
            public const string Cancelled = "Order_Cancelled";
            public const string LocationShippedOnly = "Order_LocationShippedOnly";
            public const string LocationUpdated = "Order_LocationUpdated";
        }

        public static class Product
        {
            public const string NotFound = "Product_NotFound";
            public const string Created = "Product_Created";
            public const string Updated = "Product_Updated";
            public const string Deleted = "Product_Deleted";
            public const string VendorNotApproved = "Product_VendorNotApproved";
            public const string NotVendor = "Product_NotVendor";
            public const string NoVendor = "Product_NoVendor";
            public const string DeleteNotVendor = "Product_DeleteNotVendor";
            public const string StockNotVendor = "Product_StockNotVendor";
            public const string StockUpdated = "Product_StockUpdated";
            public const string Activated = "Product_Activated";
            public const string Deactivated = "Product_Deactivated";
            public const string AdminCreated = "Product_AdminCreated";
            public const string AdminUpdated = "Product_AdminUpdated";
            public const string AdminDeleted = "Product_AdminDeleted";
            public const string StockAdminUpdated = "Product_StockAdminUpdated";
        }

        public static class Review
        {
            public const string NotFound = "Review_NotFound";
            public const string NotFoundSimple = "Review_NotFoundSimple";
            public const string Submitted = "Review_Submitted";
            public const string Deleted = "Review_Deleted";
            public const string Reported = "Review_Reported";
            public const string Approved = "Review_Approved";
            public const string Rejected = "Review_Rejected";
        }

        public static class Report
        {
            public const string DateRangeRequired = "Report_DateRangeRequired";
            public const string NotFoundOrResolved = "Report_NotFoundOrResolved";
            public const string Approved = "Report_Approved";
            public const string Dismissed = "Report_Dismissed";
            public const string ReviewDeleted = "Report_ReviewDeleted";
        }

        public static class User
        {
            public const string NotFound = "User_NotFound";
            public const string CannotDeleteSelf = "User_CannotDeleteSelf";
            public const string Deleted = "User_Deleted";
            public const string Unlocked = "User_Unlocked";
            public const string Locked = "User_Locked";
            public const string Created = "User_Created";
            public const string Updated = "User_Updated";
            public const string AdminPasswordReset = "User_AdminPasswordReset";
            public const string PasswordReset = "User_PasswordReset";
        }

        public static class Vendor
        {
            public const string NotFound = "Vendor_NotFound";
            public const string AlreadyExists = "Vendor_AlreadyExists";
            public const string NameExists = "Vendor_NameExists";
            public const string Created = "Vendor_Created";
            public const string Updated = "Vendor_Updated";
            public const string Deleted = "Vendor_Deleted";
            public const string Activated = "Vendor_Activated";
            public const string Deactivated = "Vendor_Deactivated";
            public const string Approved = "Vendor_Approved";
            public const string Rejected = "Vendor_Rejected";
            public const string RejectionRequired = "Vendor_RejectionRequired";
            public const string OrderNotFound = "Vendor_OrderNotFound";
            public const string AdminRequired = "Vendor_AdminRequired";
            public const string NotApproved = "Vendor_NotApproved";
        }

        public static class Role
        {
            public const string NotFound = "Role_NotFound";
        }

        public static class Variant
        {
            public const string Added = "Variant_Added";
            public const string Updated = "Variant_Updated";
            public const string Deleted = "Variant_Deleted";
            public const string NotVendor = "Variant_NotVendor";
            public const string VendorNotApproved = "Variant_VendorNotApproved";
            public const string TypeUpdated = "Variant_TypeUpdated";
            public const string TypeDeleted = "Variant_TypeDeleted";
        }

        public static class Wishlist
        {
            public const string Added = "Wishlist_Added";
            public const string Removed = "Wishlist_Removed";
            public const string Cleared = "Wishlist_Cleared";
        }

        public static class Notification
        {
            public const string MarkedRead = "Notification_MarkedRead";
            public const string AllMarkedRead = "Notification_AllMarkedRead";
            public const string Deleted = "Notification_Deleted";
            public const string BulkDeleted = "Notification_BulkDeleted";
        }
    }
}
