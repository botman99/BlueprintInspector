using System;

namespace BlueprintInspector
{
    class GuidAndCmdID
    {
        public const string PackageGuidString = "9fe6d913-c6f4-4eee-99d5-23c22924ac10";
        public const string PackageCmdSetGuidString = "faaf1a9b-f925-4bfb-b76c-7d6d9e9968d1";

        public static readonly Guid guidPackage = new Guid(PackageGuidString);
        public static readonly Guid guidCmdSet = new Guid(PackageCmdSetGuidString);

        public const uint cmdidGenerateJsonFile = 0x0100;
        public const uint cmdidCopyToClipboard = 0x0101;
        public const uint cmdidOpenAssetPath = 0x0102;
    }
}
