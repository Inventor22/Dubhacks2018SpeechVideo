using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dubhacks2018AVWinForms
{
    public partial class Charlie : Form
    {
        public Charlie()
        {
            InitializeComponent();
        }

        private void UpdateLog(string value)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(UpdateLog), new object[] { value });
                return;
            }
            outputTextbox.AppendText(value + "\n");
        }

        private void UpdateLog2(double happiness)
        {

            if (this.InvokeRequired)
            {
                this.Invoke(new Action<double>(UpdateLog2), new object[] { happiness });
                return;
            }
            if (happiness > 0.90)
            {
                textBox1.Text = "You're doing great! Keep Smiling!"  + "\n";
                textBox1.ForeColor = Color.Green;
            }
            else
            {
                textBox1.Text = "Try to smile more!";
                textBox1.ForeColor = Color.Red;
            }
        }
        private async void videoButton_Click(object sender, EventArgs e)
        {
            int size = -1;
            DialogResult result = openFileDialog1.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                string file = openFileDialog1.FileName;
                try
                {
                    videoFileTextBox.Text = file;
                    //string text = File.ReadAllText(file);
                    //size = text.Length;

                    await DoMagic(file);
                }
                catch (IOException)
                {
                }
            }
            Console.WriteLine(size); // <-- Shows file size in debugging mode.
            Console.WriteLine(result); // <-- For debugging use.
        }

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

        async Task DoMagic(string fileName)
        {
            try
            {
                await Task.Factory.StartNew(async () =>
                {
                    string outputFormat = Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\img\frame{0}_{1}.jpg");
                    var inputFile = new MediaFile { Filename = fileName };
                    var audioFile = new MediaFile { Filename = Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\audio\", "Audio.wav") };

                    Dictionary<string, int> wordFrequency = new Dictionary<string, int>();

                    using (var engine = new Engine())
                    {
                        int x = 0;
                        Console.WriteLine(inputFile.Filename);
                        UpdateLog(inputFile.Filename + "\n");
                        engine.GetMetadata(inputFile);
                        engine.Convert(inputFile, audioFile);


                        string text = ExtractTextFromSpeech(audioFile.Filename).Result;
                        Console.WriteLine("Analyzing parsed text: " + text);
                        UpdateLog("Analyzing parsed text: " + text + "\n");

                        text = text.ToLowerInvariant();
                        foreach (string word in text.Split(" !@#%^&*()_?-.,".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (wordFrequency.ContainsKey(word))
                            {
                                wordFrequency[word]++;
                            }
                            else
                            {
                                wordFrequency.Add(word, 1);
                            }
                        }
                        var ordered = wordFrequency.OrderByDescending(entry => entry.Value).Take(3);

                        UpdateLog("Your most frequently used words are: ");
                        //Console.WriteLine("Your most frequently used words are: ");
                        foreach (var val in ordered)
                        {
                            //Console.WriteLine();

                            UpdateLog(val.Key + ": " + val.Value);
                        }

                        for (int i = 0; i < inputFile.Metadata.Duration.TotalMilliseconds; i += msFrameIncrement)
                        {
                            var options = new ConversionOptions { Seek = TimeSpan.FromMilliseconds(i) };

                            var outputFile = new MediaFile { Filename = string.Format(outputFormat, x++, i) };

                            engine.GetThumbnail(inputFile, outputFile, options);
                        }

                        // Get emotion from face pics
                        Console.WriteLine("Getting emotion from face...");
                        UpdateLog("Getting emotion from face...");
                        FaceClient faceClient = new FaceClient(
                            new ApiKeyServiceClientCredentials(faceSubscriptionKey),
                            new System.Net.Http.DelegatingHandler[] { })
                        {
                            Endpoint = faceEndpoint
                        };

                        string dir = Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\img\");
                        // UpdateLog(dir);
                        string[] imgPaths = Directory.GetFiles(dir, "*.jpg");
                        List<FaceAttributes> attribs = new List<FaceAttributes>();
                        List<Task> tasks = new List<Task>();

                        foreach (string imgPath in imgPaths)
                        {
                            Image image = new Bitmap(new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read));

                            pictureBox1.Image = image;
                            //tasks.Add(DetectLocalAsync(faceClient, imgPath, attribs));
                            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;

                            DetectLocalAsync(faceClient, imgPath, attribs).Wait();
                        }


                        Task.WaitAll(tasks.ToArray());
                        Console.WriteLine("Attribs Count = " + attribs.Count());
                        double happiness = attribs.Average(a => a.Smile.Value);
                        // Console.WriteLine($"Happy? ({happiness}): " + (happiness > 0.5 ? "Yeah buddy" : "Nope, Grumpy cat"));

                        UpdateLog($"Happy? ({happiness}): " + (happiness > 0.5 ? "Yeah buddy" : "Nope, Grumpy cat"));
                        UpdateLog2(happiness);
                        UpdateLog("Your most frequently used words are: ");
                        //Console.WriteLine("Your most frequently used words are: ");
                        foreach (var val in ordered)
                        {
                            //Console.WriteLine();

                            UpdateLog(val.Key + ": " + val.Value);
                        }

                    }
                });
            }
            catch (Exception ex)
            {
                UpdateLog(ex.Message);
                if (ex.InnerException != null)
                {
                    UpdateLog(ex.InnerException.Message);
                }
            }
        }

        // Detect faces in a local image
        private async Task DetectLocalAsync(FaceClient faceClient, string imagePath, List<FaceAttributes> attribs)
        {
            if (!File.Exists(imagePath))
            {
                Console.WriteLine(
                    "\nUnable to open or read localImagePath:\n{0} \n", imagePath);
                return;
            }

            try
            {
                using (Stream imageStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    IList<DetectedFace> faceList =
                               await faceClient.Face.DetectWithStreamAsync(
                                   image: imageStream,
                                   returnFaceId: true,
                                   returnFaceLandmarks: false,
                                   returnFaceAttributes: faceAttributes);

                    string a = string.Format("{0}:\n{1}\n",
                        imagePath.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Last(),
                        GetFaceAttributes(faceList.Count > 0 ? faceList[0] : null, attribs));
                    Console.WriteLine(a);
                    UpdateLog(a);

                }

            }
            catch (APIErrorException e)
            {
                Console.WriteLine(imagePath + ": " + e.Message);
            }

        }

        private string GetFaceAttributes(DetectedFace face, List<FaceAttributes> attribs)
        {
            if (face == null) return string.Empty;

            StringBuilder attributes = new StringBuilder();

            attribs.Add(face.FaceAttributes);

            attributes.AppendFormat("{0}: {1}, ", FaceAttributeType.Age.ToString(), face.FaceAttributes.Age);
            attributes.AppendFormat("{0}: {1}, ", FaceAttributeType.Gender.ToString(), face.FaceAttributes.Gender);
            attributes.AppendFormat("{0}: {1}\n", FaceAttributeType.Smile.ToString(), face.FaceAttributes.Smile);
            List<string> b = new List<string>();
            foreach (PropertyInfo a in face.FaceAttributes.Emotion.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                b.Add(string.Format("{0}: {1}", a.Name, face.FaceAttributes.Emotion.GetType().GetProperty(a.Name).GetValue(face.FaceAttributes.Emotion)));
            }
            attributes.AppendLine(string.Join(", ", b));

            return attributes.ToString();
        }

        async Task<string> ExtractTextFromSpeech(string file)
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

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
