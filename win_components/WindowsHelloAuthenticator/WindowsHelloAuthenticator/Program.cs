﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;

namespace WindowsHelloAuthenticator
{
    class Program
    {

        // Exit codes
        public const byte ERR_VERIFY_HELLO_NOT_SUPPORTED = 170; // Avoid reserved exit codes of UNIX
        public const byte ERR_VERIFY_CREDENTIAL_EXISTS = 171;
        public const byte ERR_VERIFY_CREDENTIAL_NOT_FOUND = 172;
        public const byte ERR_VERIFY_DEVICE_IS_LOCKED = 173;
        public const byte ERR_VERIFY_UNKNOWN_ERR = 175;         // Skip 174 because the number should correspond to KeyCredentialStatus.Success if exists 
        public const byte ERR_VERIFY_USER_CANCELLED = 176;
        public const byte ERR_VERIFY_USER_PREFS_PASSWD = 177;

        static int CredentialStatusToExitCode(KeyCredentialStatus status)
        {
            return 171 + (int)status; // Avoid reserved exit codes of UNIX
        }

        static string ExitCodeToMessage(int code, string key_name)
        {
            switch (code)
            {
                case 0:
                    return "Success.";
                case ERR_VERIFY_HELLO_NOT_SUPPORTED:
                    return "Windows Hello is not supported in this device.";
                case ERR_VERIFY_CREDENTIAL_EXISTS:
                    return "The credential already exists. Creation failed.";
                case ERR_VERIFY_CREDENTIAL_NOT_FOUND:
                    return "The credential '" + key_name + "' does not exist.";
                case ERR_VERIFY_DEVICE_IS_LOCKED:
                    return "The Windows Hello security device is locked.";
                case ERR_VERIFY_UNKNOWN_ERR:
                    return "Unknown error.";
                case ERR_VERIFY_USER_CANCELLED:
                    return "The user cancelled.";
                case ERR_VERIFY_USER_PREFS_PASSWD:
                    return "The user prefers to enter password. Aborted.";
                default:
                    return "Unkwon internal error.";
            }
        }

        static async Task<(int err, byte[] sig)> VerifyUser(string key_name, string contentToSign)
        {
            if (await KeyCredentialManager.IsSupportedAsync() == false)
            {
                await (new MessageDialog("KeyCredentialManager not supported")).ShowAsync();
                return (ERR_VERIFY_HELLO_NOT_SUPPORTED, null);
            }

            var key = await KeyCredentialManager.OpenAsync(key_name);
            if (key.Status != KeyCredentialStatus.Success)
            {
                return (CredentialStatusToExitCode(key.Status), null);
            }

            var buf = CryptographicBuffer.ConvertStringToBinary(contentToSign, BinaryStringEncoding.Utf8);
            var signRes = await key.Credential.RequestSignAsync(buf);
            if (signRes.Status != KeyCredentialStatus.Success)
            {
                return (CredentialStatusToExitCode(key.Status), null);
            }

            byte[] sig;
            CryptographicBuffer.CopyToByteArray(signRes.Result, out sig);
            return (0, sig);
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: WindowsHelloAuthenticator.exe credential_key_name");
                Console.WriteLine("");
                Console.WriteLine("This program authenticates the current user by Windows Hello,");
                Console.WriteLine("and outputs a signature of the signed input from stdin to stdout.");
                Console.WriteLine("The input will be signed by a private key that is associated with 'credential_key_name'");
                Environment.Exit(1);
            }

            var verifyRes = VerifyUser(args[0], Console.In.ReadToEnd()).Result;
            if (verifyRes.err > 0)
            {
                Console.WriteLine(ExitCodeToMessage(verifyRes.err, args[0]));
                Environment.Exit(verifyRes.err);
            }
            var stdout = Console.OpenStandardOutput();
            stdout.Write(verifyRes.sig, 0, verifyRes.sig.Length);
        }
    }
}
