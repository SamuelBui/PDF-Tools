using System.Reflection;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfTool.App.Helpers;

public static class PdfSecurityAccessHelper
{
    public static bool HasOwnerPermissions(PdfDocument document, string? password)
    {
        if (!document.SecuritySettings.IsEncrypted)
        {
            return true;
        }

        var securitySettings = document.SecuritySettings;

        var hasOwnerPermissionsProperty = securitySettings.GetType().GetProperty(
            "HasOwnerPermissions",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var hasOwnerPermissionsGetter = hasOwnerPermissionsProperty?.GetGetMethod(true);
        if (hasOwnerPermissionsGetter != null)
        {
            if (hasOwnerPermissionsGetter.Invoke(securitySettings, null) is bool hasOwnerPermissions)
            {
                return hasOwnerPermissions;
            }
        }

        var handler = GetPropertyValue(securitySettings, "EffectiveSecurityHandler")
                      ?? GetPropertyValue(securitySettings, "SecurityHandler");

        if (handler == null)
        {
            return false;
        }

        var validatePasswordMethod = handler.GetType().GetMethod(
            "ValidatePassword",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);

        if (validatePasswordMethod == null)
        {
            return false;
        }

        var result = validatePasswordMethod.Invoke(handler, new object?[] { password ?? string.Empty });
        return string.Equals(result?.ToString(), "OwnerPassword", StringComparison.Ordinal);
    }

    public static bool TryHasOwnerLevelAccess(string filePath, string? password, out string message)
    {
        message = string.Empty;

        try
        {
            using var document = PdfReader.Open(filePath, password ?? string.Empty, PdfDocumentOpenMode.Modify, new PdfReaderOptions());
            if (document.CanSave(ref message))
            {
                return true;
            }

            if (HasOwnerPermissions(document, password))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                message = "Owner password is required to remove PDF protection and permission restrictions.";
            }

            return false;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private static object? GetPropertyValue(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return property?.GetValue(instance);
    }
}
