using Lab_Feedback_WPF.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lab_Feedback_WPF.Models
{
    public class Student
    {
        public string FirstName { get; }
        public string LastName { get; }
        public string IdNumber { get; }

        public string? Folder { get; }

        public string FullName => LastName + ", " + FirstName;

        public Student(string firstName, string lastName, string idNumber, string? folder)
        {
            FirstName = firstName;
            LastName = lastName;
            IdNumber = idNumber;
            Folder = folder;
        }

        public Student(string path, string? studentFolder)
        {
            var cleanPath = FileHandler.GetSubfolderFromPath(path, studentFolder);

            var splitName = cleanPath?.Split('_');
            var firstNameAndId = splitName is { Length: >= 2 } ? splitName[1].Split('-', 2) : null;
            if (splitName is not { Length: >= 2 } || firstNameAndId is not { Length: 2 }
                || string.IsNullOrWhiteSpace(splitName[0])
                || string.IsNullOrWhiteSpace(firstNameAndId[0])
                || string.IsNullOrWhiteSpace(firstNameAndId[1]))
            {
                throw new ArgumentException("Student folder must use the Last_First-ID naming format.", nameof(studentFolder));
            }

            LastName = splitName[0];
            FirstName = firstNameAndId[0];
            IdNumber = firstNameAndId[1];
            Folder = studentFolder;
        }

        public static List<Student> GetStudentsFromFolders(string path)
        {
            List<Student> folders = new();

            try
            {
                if (Directory.Exists(path))
                {
                    string?[] subFolders = Directory.GetDirectories(path);


                    foreach (var subFolder in subFolders)
                    {
                        try
                        {
                            folders.Add(new Student(path, subFolder));
                        }
                        catch (ArgumentException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipping invalid student folder: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // TODO: Update this to WPF equivalent
                    // MessageBox.Show("Error");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unable to load student folders: {ex.Message}");
            }

            return folders;
        }
    }
}
