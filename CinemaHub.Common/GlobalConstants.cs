﻿namespace CinemaHub.Common
{
    using System.Collections.Generic;

    public static class GlobalConstants
    {
        public const string SystemName = "CinemaHub";

        public const string AdministratorRoleName = "Administrator";

        public const string DefaultAvatarImagePath = "\\images\\template\\uploads\\user-img.png";

        public const string TemporaryImagesDir = "\\images\\temporary\\";

        public static readonly Dictionary<string, List<byte[]>> ImageFileSignatures = new Dictionary<string, List<byte[]>>
                                                                                     {
                                                                                         {
                                                                                            "jpeg", new List<byte[]>
                                                                                                        {
                                                                                                            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                                                                                                            new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 },
                                                                                                            new byte[] { 0xFF, 0xD8, 0xFF, 0xE3 },
                                                                                                        }
                                                                                         },
                                                                                         {
                                                                                            "jpg", new List<byte[]>
                                                                                                        {
                                                                                                            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                                                                                                            new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 },
                                                                                                            new byte[] { 0xFF, 0xD8, 0xFF, 0xE3 },
                                                                                                        }
                                                                                         },
                                                                                         {
                                                                                            "png", new List<byte[]>
                                                                                                        {
                                                                                                            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
                                                                                                        }
                                                                                         },
                                                                                     };

        public static readonly string CreateOperationName = "Create";
        public static readonly string ReadOperationName = "Read";
        public static readonly string UpdateOperationName = "Update";
        public static readonly string DeleteOperationName = "Delete";
        public static readonly string ApproveOperationName = "Approve";
        public static readonly string RejectOperationName = "Reject";
    }
}
