using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace BotwUnpacker;

public static class JsonConfiguration
{
    public static IConfiguration ConfigurationContainer { get; private set; }

    public static IConfiguration CreateConfigurationContainer()
    {
        try
        {
            return ConfigurationContainer = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json").Build();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }
    
    public static bool Set(this IConfiguration configuration, string key, string value)
    {
        try
        {
            configuration[key] = value;
            
            var filePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            string json = File.ReadAllText(filePath);
            dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                
            var sectionPath = key.Split(":")[0];

            if (!string.IsNullOrEmpty(sectionPath)) 
            {
                var keyPath = key.Split(":")[1];
                jsonObj[sectionPath][keyPath] = value;
            }
            else 
            {
                jsonObj[sectionPath] = value; // if no sectionpath just set the value
            }

            string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filePath, output);
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }
    }
    
    public static void AddOrUpdateSetting<T>(this IConfiguration configuration, string sectionPathKey, T value)
    {
        try
        {
            configuration[sectionPathKey] = Newtonsoft.Json.JsonConvert.SerializeObject(value, Newtonsoft.Json.Formatting.Indented);
            
            var filePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            string json = File.ReadAllText(filePath);
            dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

            SetValueRecursively(sectionPathKey, jsonObj, value);

            string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filePath, output);

        }
        catch (Exception ex)
        {
            Console.WriteLine("Error writing app settings | {0}", ex.Message);
        }
    }

    private static void SetValueRecursively<T>(string sectionPathKey, dynamic jsonObj, T value)
    {
        // split the string at the first ':' character
        var remainingSections = sectionPathKey.Split(":", 2);

        var currentSection = remainingSections[0];
        if (remainingSections.Length > 1)
        {
            // continue with the procress, moving down the tree
            var nextSection = remainingSections[1];
            SetValueRecursively(nextSection, jsonObj[currentSection], value);
        }
        else
        {
            // we've got to the end of the tree, set the value
            jsonObj[currentSection] = value; 
        }
    }
}