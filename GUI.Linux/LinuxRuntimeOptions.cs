using System;
using System.Reflection;
using GameRes;

namespace GARbro.GUI.Linux;

internal static class LinuxRuntimeOptions
{
    public static string Password { get; set; }

    public static bool TryApplyDefaultOptions(object sender, ParametersRequestEventArgs args)
    {
        var resource = sender as IResource;
        var options = resource?.GetDefaultOptions();
        if (options == null)
        {
            args.InputResult = false;
            return false;
        }

        ApplyPassword(options);
        args.Options = options;
        args.InputResult = true;
        return true;
    }

    private static void ApplyPassword(ResourceOptions options)
    {
        var password = FirstNonEmpty(Password, Environment.GetEnvironmentVariable("GARBRO_PASSWORD"));
        if (string.IsNullOrEmpty(password))
        {
            return;
        }

        var type = options.GetType();
        foreach (var name in new[] { "Password", "PassPhrase", "Keyword", "Key" })
        {
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite && property.PropertyType == typeof(string))
            {
                property.SetValue(options, password);
                return;
            }

            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(string))
            {
                field.SetValue(options, password);
                return;
            }
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return null;
    }
}
