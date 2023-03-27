using Episerver.Labs.Cognitive.Attributes;
using EPiServer.Core;
using EPiServer.Framework.Blobs;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Options;
using Optimizely.Labs.ComputerVision;
using System.Drawing;
using System.Reflection;

namespace Episerver.Labs.Cognitive
{
    public class VisionHandler
    {
        private readonly IBlobFactory _blobFactory;

        private readonly ComputerVisionClient _computeVisionClient;

        private readonly ComputeVisionOptions _computeVisionOptions;

        public VisionHandler(IOptions<ComputeVisionOptions> computeVisionOptions, IBlobFactory blobFactory)
        {
            _blobFactory = blobFactory;
            _computeVisionOptions = computeVisionOptions.Value;
            _computeVisionClient = new ComputerVisionClient(new ApiKeyServiceClientCredentials(_computeVisionOptions.SubscriptionKey))
            {
                Endpoint = _computeVisionOptions.Endpoint
            };
        }

        public bool Enabled => _computeVisionClient != null;

        public void HandleImage(ImageData img)
        {
            var thumbs = img.GetEmptyPropertiesWithAttribute(typeof(SmartThumbnailAttribute));

            //TODO: Solve problem here.
            var propertyList = img.GetEmptyPropertiesWithAttribute(typeof(VisionAttribute)).ToList();
            var ocrs = img.GetEmptyPropertiesWithAttribute(typeof(VisionAttribute))
                .Where(prop => prop.GetCustomAttributes()
                    .Where(ca => ca is VisionAttribute)
                    .Cast<VisionAttribute>()
                    .First().VisionType == VisionTypes.Text)
                .ToList();

            var descriptions = img.GetEmptyPropertiesWithAttribute(typeof(VisionAttribute))
                .Where(prop => prop.GetCustomAttributes()
                    .Where(ca => ca is VisionAttribute).Cast<VisionAttribute>()
                    .First().VisionType != VisionTypes.Text)
                .ToList();

            if (thumbs.Any() || ocrs.Any() || descriptions.Any())
            {
                //Size image properly
                Stream strm = img.BinaryData.OpenRead();
                if (strm.Length >= 4000000)
                {
                    var g = ScaleImage(Image.FromStream(strm), 500, 500); //TODO: Change this to make largest possible size smaller than 4 mb
                    strm = new MemoryStream(4000000);
                    g.Save(strm, System.Drawing.Imaging.ImageFormat.Jpeg);
                    strm.Seek(0, SeekOrigin.Begin);
                }
                BinaryReader reader = new BinaryReader(strm);
                byte[] bytes = reader.ReadBytes((int)strm.Length);
                reader.Close();

                //Handle thumbnails
                List<Task> tasks = new List<Task>();
                if (thumbs.Any()) tasks.Add(Task.Run(() => GenerateThumbnails(img, new MemoryStream(bytes), thumbs)));//tasks.Add(GenerateThumbnails(img, strm, thumbs));

                //handle OCR
                if (ocrs.Any())
                {
                    tasks.Add(Task.Run(() => GenerateOCR(img, new MemoryStream(bytes), ocrs)));
                }

                //handle descriptions
                if (descriptions.Any())
                {
                    tasks.Add(Task.Run(() => TagAndDescription(img, new MemoryStream(bytes), descriptions)));
                }

                //Complete all.

                Task.WaitAll(tasks.ToArray());
            }
        }

        public async Task TagAndDescription(ImageData img, Stream strm, IEnumerable<PropertyInfo> props)
        {
            var res = await AnalyzeImage(strm);

            foreach (var p in props)
            {
                var atb = p.GetCustomAttribute<VisionAttribute>();
                switch (atb.VisionType)
                {
                    case VisionTypes.Adult:
                        if (p.PropertyType == typeof(bool))
                        {
                            p.SetValue(img, res.Adult.IsAdultContent);
                        }
                        else if (p.PropertyType == typeof(double))
                        {
                            p.SetValue(img, res.Adult.AdultScore);
                        }
                        break;

                    case VisionTypes.Categories:
                        var catlist = res.Categories.Select(c => c.Name).ToArray();
                        if (p.PropertyType == typeof(string[]))
                        {
                            p.SetValue(img, catlist);
                        }
                        else if (p.PropertyType == typeof(string))
                        {
                            p.SetValue(img, string.Join(atb.Separator, catlist));
                        }
                        break;

                    case VisionTypes.ClipArt:
                        if (p.PropertyType == typeof(bool))
                        {
                            p.SetValue(img, (res.ImageType.ClipArtType == 0));
                        }
                        break;

                    case VisionTypes.Description:
                        //Handle string array with multiple captions.
                        if (p.PropertyType == typeof(string))
                        {
                            p.SetValue(img, res.Description.Captions.Select(d => d.Text).FirstOrDefault());
                        }
                        else if (p.PropertyType == typeof(string[]))
                        {
                            p.SetValue(img, res.Description.Captions.Select(d => d.Text).ToArray());
                        }
                        break;

                    case VisionTypes.LineDrawing:
                        if (p.PropertyType == typeof(bool))
                        {
                            p.SetValue(img, (res.ImageType.LineDrawingType == 0));
                        }
                        break;

                    case VisionTypes.Racy:
                        if (p.PropertyType == typeof(bool))
                        {
                            p.SetValue(img, res.Adult.IsRacyContent);
                        }
                        else if (p.PropertyType == typeof(double))
                        {
                            p.SetValue(img, res.Adult.RacyScore);
                        }
                        break;

                    case VisionTypes.Tags:
                        if (p.PropertyType == typeof(string))
                        {
                            p.SetValue(img, string.Join(atb.Separator, res.Tags.Select(t => t.Name).ToArray()));
                        }
                        else if (p.PropertyType == typeof(string[]))
                        {
                            p.SetValue(img, res.Tags.Select(t => t.Name).ToArray());
                        }
                        else if (p.PropertyType == typeof(IList<string>))
                        {
                            p.SetValue(img, res.Tags.Select(t => t.Name).ToList());
                        }
                        break;

                    case VisionTypes.BlackAndWhite:
                        if (p.PropertyType == typeof(bool))
                        {
                            p.SetValue(img, res.Color.IsBWImg);
                        }
                        break;

                    case VisionTypes.AccentColor:
                        if (p.PropertyType == typeof(string))
                        {
                            p.SetValue(img, res.Color.AccentColor);
                        }
                        break;

                    case VisionTypes.DominantBackgroundColor:
                        if (p.PropertyType == typeof(string))
                        {
                            p.SetValue(img, res.Color.DominantColorBackground);
                        }
                        break;

                    case VisionTypes.DominantForegroundColor:
                        if (p.PropertyType == typeof(string))
                        {
                            p.SetValue(img, res.Color.DominantColorForeground);
                        }
                        break;

                    case VisionTypes.Faces:
                        var fcs = res.Faces.Select(fc => fc.Gender + " " + fc.Age.ToString()).ToArray();
                        //TODO: Handle if no faces, so it won't fire again.
                        if (p.PropertyType == typeof(string))
                        {
                            p.SetValue(img, string.Join(atb.Separator, fcs));
                        }
                        else if (p.PropertyType == typeof(string[]))
                        {
                            p.SetValue(img, fcs);
                        }
                        break;

                    case VisionTypes.FacesAge:
                        var ages = res.Faces.Select(fc => fc.Age.ToString()).ToArray();
                        if (p.PropertyType == typeof(string))
                        {
                            p.SetValue(img, string.Join(atb.Separator, ages));
                        }
                        else if (p.PropertyType == typeof(string[]))
                        {
                            p.SetValue(img, ages);
                        }
                        break;

                    case VisionTypes.FacesGender:
                        var genders = res.Faces.Select(fc => fc.Gender).ToArray();
                        if (p.PropertyType == typeof(string))
                        {
                            p.SetValue(img, string.Join(atb.Separator, genders));
                        }
                        else if (p.PropertyType == typeof(string[]))
                        {
                            p.SetValue(img, genders);
                        }
                        break;

                    default:
                        break;
                }
            }
            return;
        }

        public async Task GenerateOCR(ImageData img, Stream strm, IEnumerable<PropertyInfo> props)
        {
            var ocrres = await IdentifyText(strm);
            var txt = ocrres.Regions.SelectMany(r => r.Lines).Select(l => string.Join(" ", l.Words.Select(w => w.Text).ToArray())).ToArray();
            foreach (var p in props)
            {
                var atb = p.GetCustomAttribute<VisionAttribute>();
                if (p.PropertyType == typeof(string[]))
                {
                    //return multiple lines
                    p.SetValue(img, txt);
                }
                else if (p.PropertyType == typeof(string))
                {
                    //Combine into 1 line
                    p.SetValue(img, string.Join(atb.Separator, txt));
                }
                else if (p.PropertyType == typeof(XhtmlString))
                {
                    //TODO: Check if this is even called? If so, build html based representation of the regions.
                    string s = string.Join("", txt.Select(t => "<p>" + t + "</p>"));
                    p.SetValue(img, new XhtmlString(s));
                }
            }
        }

        public async Task GenerateThumbnails(ImageData img, Stream strm, IEnumerable<PropertyInfo> props)
        {
            foreach (var p in props)
            {
                if (p.PropertyType == typeof(Blob))
                {
                    var smartThumbAttribute = p.GetCustomAttribute<SmartThumbnailAttribute>();
                    using var stream = await MakeSmartThumbnail(strm, smartThumbAttribute.Width, smartThumbAttribute.Height);
                    var blob = _blobFactory.CreateBlob(img.BinaryDataContainer, Path.GetExtension(img.BinaryData.ID.ToString()));
                    using Stream outstream = blob.OpenWrite();
                    await stream.CopyToAsync(outstream);
                    outstream.Close();
                    p.SetValue(img, blob);
                }
            }
        }

        public async Task<OcrResult> IdentifyText(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            return await _computeVisionClient.RecognizePrintedTextInStreamAsync(false, stream);
        }

        public async Task<ImageAnalysis> AnalyzeImage(Stream stream)
        {
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                var list = new List<VisualFeatureTypes?>()
                {
                    VisualFeatureTypes.Description,
                    VisualFeatureTypes.Tags,
                    VisualFeatureTypes.Adult,
                    VisualFeatureTypes.Categories,
                    VisualFeatureTypes.ImageType,
                    VisualFeatureTypes.Color,
                    VisualFeatureTypes.Faces
                };

                return await _computeVisionClient.AnalyzeImageInStreamAsync(stream, list);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task<Stream> MakeSmartThumbnail(Stream s, int x, int y)
        {
            s.Seek(0, SeekOrigin.Begin);
            return await _computeVisionClient.GenerateThumbnailInStreamAsync(x, y, s);
        }

        //TODO: Resize so max size is <4 MB. Guess which scale that is based on current bytesize and w+h.
        public static Image ScaleImage(Image image, int maxWidth, int maxHeight)
        {
            var ratioX = (double)maxWidth / image.Width;
            var ratioY = (double)maxHeight / image.Height;

            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(image.Width * ratio);
            var newHeight = (int)(image.Height * ratio);

            var newImage = new Bitmap(newWidth, newHeight);
            using (var graphics = Graphics.FromImage(newImage))
            {
                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }
            return newImage;
        }
    }
}