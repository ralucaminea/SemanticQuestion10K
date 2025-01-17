﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Reflection;

namespace RepoUtils;

internal static class RepoFiles
{
    /// <summary>
    /// Scan the local folders from the repo, looking for "samples/skills" folder.
    /// </summary>
    /// <returns>The full path to samples/skills</returns>
    internal static string SampleSkillsPath()
    {
        const string PARENT = "samples";
        const string FOLDER = "skills";

        bool SearchPath(string pathToFind, out string result, int maxAttempts = 10)
        {
            var currDir = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
            bool found;
            do
            {
                result = Path.Join(currDir, pathToFind);
                found = Directory.Exists(result);
                currDir = Path.GetFullPath(Path.Combine(currDir, ".."));
            } while (maxAttempts-- > 0 && !found);

            return found;
        }

        if (!SearchPath(PARENT + Path.DirectorySeparatorChar + FOLDER, out string path)
            && !SearchPath(FOLDER, out path))
        {
            throw new YourAppException("Skills directory not found. The app needs the skills from the repo to work.");
        }

        return path;
    }

}

public static class JsonFileReader
{
    public static T Read<T>(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        return JsonSerializer.Deserialize<T>(stream);
    }
}