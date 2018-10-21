using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Dubhack2018SpeechVideo
{
    class Program
    {
        private const string speechSubscritionKey = "ce0969fe454948f1b349b169853770c5";
        private const string faceSubscriptionKey = "3c9fd75de5cf47c8840ee93e93bfd295";
        private const string faceEndpoint = "https://westus.api.cognitive.microsoft.com";

        private const int msFrameIncrement = 500; // ms

        private static readonly FaceAttributeType[] faceAttributes =
        {
            FaceAttributeType.Age,
            FaceAttributeType.Gender,
            FaceAttributeType.Emotion,
            FaceAttributeType.Glasses,
            FaceAttributeType.Hair,
            FaceAttributeType.Smile
        };

        static void Main(string[] args)
        {
            string outputFormat = @"C:\Users\Dustin Dobransky\source\repos\Dubhack2018SpeechVideo\Dubhack2018SpeechVideo\img\frame{0}_{1}.jpg";
            var inputFile = new MediaFile { Filename = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\vid\"), "*.mp4").FirstOrDefault() };
            var audioFile = new MediaFile { Filename = Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\audio\", "Audio.wav") };

            Dictionary<string, int> wordFrequency = new Dictionary<string, int>();

            using (var engine = new Engine())
            {
                int x = 0;
                Console.WriteLine(inputFile.Filename);
                engine.GetMetadata(inputFile);
                engine.Convert(inputFile, audioFile);

                string text = ExtractTextFromSpeech(audioFile.Filename).Result;
                Console.WriteLine("Analyzing parsed text: " + text);
                text = text.ToLowerInvariant();
                foreach(string word in text.Split(" !@#%^&*()_?-.,".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                {
                    if (wordFrequency.ContainsKey(word))
                    {
                        wordFrequency[word] ++;
                    } else
                    {
                        wordFrequency.Add(word, 1);
                    }
                }
                var ordered = wordFrequency.OrderByDescending(entry=>entry.Value).Take(3);
                Console.WriteLine("Your most frequently used words are: ");
                foreach(var val in ordered)
                {
                    Console.WriteLine(val.Key +": "+ val.Value);
                }

                for (int i = 0; i < inputFile.Metadata.Duration.TotalMilliseconds; i += msFrameIncrement)
                {
                    var options = new ConversionOptions { Seek = TimeSpan.FromMilliseconds(i) };

                    var outputFile = new MediaFile { Filename = string.Format(outputFormat, x++, i) };

                    engine.GetThumbnail(inputFile, outputFile, options);
                }

                // Get emotion from face pics
                Console.WriteLine("Getting emotion from face...");
                FaceClient faceClient = new FaceClient(
                    new ApiKeyServiceClientCredentials(faceSubscriptionKey),
                    new System.Net.Http.DelegatingHandler[] { })
                {
                    Endpoint = faceEndpoint
                };

                string[] imgPaths = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\img\"), "*.jpg");
                List<FaceAttributes> attribs = new List<FaceAttributes>();

                foreach (string imgPath in imgPaths)
                {
                    var t2 = DetectLocalAsync(faceClient, imgPath, attribs);
                }

                Console.WriteLine("Happy?: " + ((attribs.Average(a => a.Smile).Value > .5) ? "Yeah buddy" : "Nope, Grumpy cat"));


                Console.ReadKey();
            }
        }

        // Detect faces in a local image
        private static async Task DetectLocalAsync(FaceClient faceClient, string imagePath, List<FaceAttributes> attribs)
        {
            if (!File.Exists(imagePath))
            {
                Console.WriteLine(
                    "\nUnable to open or read localImagePath:\n{0} \n", imagePath);
                return;
            }

            try
            {
                using (Stream imageStream = File.OpenRead(imagePath))
                {
                    IList<DetectedFace> faceList =
                            await faceClient.Face.DetectWithStreamAsync(
                                image: imageStream, 
                                returnFaceId: true, 
                                returnFaceLandmarks: false, 
                                returnFaceAttributes: faceAttributes);
                    
                    Console.WriteLine("{0}:\n{1}\n",
                        imagePath.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Last(),
                        GetFaceAttributes(faceList.Count > 0 ? faceList[0] : null, attribs));
                }
            }
            catch (APIErrorException e)
            {
                Console.WriteLine(imagePath + ": " + e.Message);
            }

        }

        private static string GetFaceAttributes(DetectedFace face, List<FaceAttributes> attribs)
        {
            if (face == null) return string.Empty;

            StringBuilder attributes = new StringBuilder();

            attribs.Add(face.FaceAttributes);

            attributes.AppendFormat("{0}: {1}, ", FaceAttributeType.Age.ToString(), face.FaceAttributes.Age);
            attributes.AppendFormat("{0}: {1}, ", FaceAttributeType.Gender.ToString(), face.FaceAttributes.Gender);
            attributes.AppendFormat("{0}: {1}\n", FaceAttributeType.Smile.ToString(), face.FaceAttributes.Smile);
            List<string> b = new List<string>();
            foreach(PropertyInfo a in face.FaceAttributes.Emotion.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                b.Add(string.Format("{0}: {1}", a.Name, face.FaceAttributes.Emotion.GetType().GetProperty(a.Name).GetValue(face.FaceAttributes.Emotion)));
            }
            attributes.AppendLine(string.Join(", ", b));

            return attributes.ToString();
        }

        static async Task<string> ExtractTextFromSpeech(string file)
        {
            try
            {
                // Creates an instance of a speech config with specified subscription key and service region.
                // Replace with your own subscription key and service region (e.g., "westus").
                var config = SpeechConfig.FromSubscription(speechSubscritionKey, "westus");

                var stopRecognition = new TaskCompletionSource<int>();

                string output = string.Empty;

                // Creates a speech recognizer using file as audio input.
                // Replace with your own audio file name.
                using (var audioInput = AudioConfig.FromWavFileInput(file))
                {
                    using (var recognizer = new SpeechRecognizer(config, audioInput))
                    {
                        // Subscribes to events.
                        //recognizer.Recognizing += (s, e) =>
                        //{
                        //    Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
                        //};

                        recognizer.Recognized += (s, e) =>
                        {
                            if (e.Result.Reason == ResultReason.RecognizedSpeech)
                            {
                                //Console.WriteLine($"RECOGNIZED: Text={e.Result.Text}");
                                output = e.Result.Text;
                            }
                            else if (e.Result.Reason == ResultReason.NoMatch)
                            {
                                Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                            }
                        };

                        recognizer.Canceled += (s, e) =>
                        {
                            Console.WriteLine($"CANCELED: Reason={e.Reason}");

                            if (e.Reason == CancellationReason.Error)
                            {
                                Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                                Console.WriteLine($"CANCELED: Did you update the subscription info?");
                            }

                            stopRecognition.TrySetResult(0);
                        };

                        recognizer.SessionStarted += (s, e) =>
                        {
                            Console.WriteLine("Start Processing Audio.");
                        };

                        recognizer.SessionStopped += (s, e) =>
                        {
                            Console.WriteLine("Done Processing Audio.");
                            stopRecognition.TrySetResult(0);
                        };

                        // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
                        await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                        // Waits for completion.
                        // Use Task.WaitAny to keep the task rooted.
                        Task.WaitAny(new[] { stopRecognition.Task });

                        // Stops recognition.
                        await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);

                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return string.Empty;
            }
        }
    }
}
