using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace AWSS3Bucket
{
    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // Class to store text in an encrypted S3 object.
    class Program
    {
        private const int MaxArgs = 3;

        public static async Task Main(string[] args)
        {
            IConfiguration Configuration = new ConfigurationBuilder()
                                            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                                            .AddEnvironmentVariables()
                                            .AddCommandLine(args)
                                            .AddUserSecrets("69f9634b-0018-44aa-b600-1c7f0417a95a")
                                            .Build();
            var section = Configuration.GetSection("AwsS3Configuration");
            AWSOptions awsOptions = new AWSOptions
            {
                Credentials = new Amazon.Runtime.BasicAWSCredentials(section.GetSection("AWSAccessKey").Value, section.GetSection("AWSSecretKey").Value),
                Region = Amazon.RegionEndpoint.GetBySystemName(section.GetSection("AWSRegion").Value)
            };

            // Parse the command line and show help if necessary
            var parsedArgs = CommandLine.Parse(args);
            if ((parsedArgs.Count == 0) || (parsedArgs.Count > MaxArgs))
            {
                PrintHelp();
                return;
            }

            // Get the application parameters from the parsed arguments
            string bucketName =
              CommandLine.GetParameter(parsedArgs, null, "-b", "--bucket-name");
            string fileName =
              CommandLine.GetParameter(parsedArgs, null, "-f", "--file-name");
            string itemName =
              CommandLine.GetParameter(parsedArgs, null, "-i", "--item-name");
            if (string.IsNullOrEmpty(bucketName) || (string.IsNullOrEmpty(fileName)))
                CommandLine.ErrorExit(
                  "\nOne or more of the required arguments is missing or incorrect." +
                  "\nRun the command with no arguments to see help.");
            string filePath = @"C:\Users\ambar.ray\Desktop";
            if (!File.Exists(Path.Combine(filePath, fileName)))
                CommandLine.ErrorExit($"\nThe given file {fileName} doesn't exist.");
            if (string.IsNullOrEmpty(itemName))
                itemName = Path.GetFileName(fileName);

            //Create the S3 bucket client
            var s3Client = new AmazonS3Client(awsOptions.Credentials, awsOptions.Region);


            //Upload file
            if (!(await CheckFileExistAsync(s3Client, bucketName, fileName)))
            {
                await UploadFileAsync(s3Client, bucketName, filePath, fileName);
            }

            //Download file
            filePath = @"C:\works";
            await DownloadFileAsync(s3Client, bucketName, filePath, fileName);
        }

        //Method to upload file in a bucket
        //
        private static async Task UploadFileAsync(IAmazonS3 s3Client, string bucketName, string path, string fileName)
        {
            var fs = File.Open(Path.Combine(path, fileName), FileMode.Open);
            PutObjectRequest request = new PutObjectRequest()
            {
                InputStream = fs,
                BucketName = bucketName,
                Key = fileName
            };
            await s3Client.PutObjectAsync(request);
        }

        //Method to download file from a bucket
        //
        private static async Task DownloadFileAsync(IAmazonS3 s3Client, string bucketName, string path, string key)
        {
            GetObjectResponse response = await s3Client.GetObjectAsync(new GetObjectRequest 
            {
                 BucketName = bucketName,
                 Key = key
            });
            var fs = response.ResponseStream;
            byte[] buffer = new byte[1024];
            var fsOut = File.OpenWrite(Path.Combine(path, key));
            while (await fs.ReadAsync(buffer) != 0)
            {
                fsOut.Write(buffer);
            }
            fsOut.Close();
        }

        //Method to delete a file in a bucket
        //
        public async Task DeleteFileAsync(IAmazonS3 s3Client, string bucketName, string key)
        {
            DeleteObjectResponse response = await s3Client.DeleteObjectAsync(new DeleteObjectRequest 
            { 
                 BucketName = bucketName,
                 Key = key
            });
        }

        //Method to check existence of a file in a bucket
        //
        public static async Task<bool> CheckFileExistAsync(IAmazonS3 s3Client, string bucketName, string key)
        {
            GetObjectResponse response = await s3Client.GetObjectAsync(new GetObjectRequest 
            { 
                 BucketName = bucketName,
                 Key = key
            });
            if (response.HttpStatusCode == HttpStatusCode.OK)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        //
        // Command-line help
        private static void PrintHelp()
        {
            Console.WriteLine(
              "\nUsage: KmsS3Encryption -b <bucket-name> -f <file-name> [-i <item-name>]" +
              "\n  -b, --bucket-name: The name of an existing S3 bucket." +
              "\n  -f, --file-name: The name of a text file with content to encrypt and store in S3." +
              "\n  -i, --item-name: The name you want to use for the item." +
              "\n      If item-name isn't given, file-name will be used.");
        }

    }

    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // Class that represents a command line on the console or terminal
    // (This is the same for all examples. When you have seen it once, you can ignore it)
    static class CommandLine
    {
        // Method to parse a command line of the form: "--param value" or "-p value".
        // If "param" is found without a matching "value", Dictionary.Value is an empty string.
        // If "value" is found without a matching "param", Dictionary.Key is "--NoKeyN"
        //  where "N" represents sequential numbers.
        public static Dictionary<string, string> Parse(string[] args)
        {
            var parsedArgs = new Dictionary<string, string>();
            int i = 0, n = 0;
            while (i < args.Length)
            {
                // If the first argument in this iteration starts with a dash it's an option.
                if (args[i].StartsWith("-"))
                {
                    var key = args[i++];
                    var value = string.Empty;

                    // Is there a value that goes with this option?
                    if ((i < args.Length) && (!args[i].StartsWith("-"))) value = args[i++];
                    parsedArgs.Add(key, value);
                }

                // If the first argument in this iteration doesn't start with a dash, it's a value
                else
                {
                    parsedArgs.Add("--NoKey" + n.ToString(), args[i++]);
                    n++;
                }
            }

            return parsedArgs;
        }

        //
        // Method to get a parameter from the parsed command-line arguments
        public static string GetParameter(
          Dictionary<string, string> parsedArgs, string def, params string[] keys)
        {
            string retval = null;
            foreach (var key in keys)
                if (parsedArgs.TryGetValue(key, out retval)) break;
            return retval ?? def;
        }

        //
        // Exit with an error.
        public static void ErrorExit(string msg, int code = 1)
        {
            Console.WriteLine("\nError");
            Console.WriteLine(msg);
            Environment.Exit(code);
        }
    }
}
