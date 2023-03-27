using EPiServer.Core;
using EPiServer.Framework;
using EPiServer.Framework.Blobs;
using EPiServer.Framework.Initialization;
using EPiServer.Notification;
using EPiServer.ServiceLocation;

namespace Episerver.Labs.Cognitive;

[InitializableModule]
[ModuleDependency(typeof(EPiServer.Web.InitializationModule))]
public class CognitiveInit : IInitializableModule
{
    protected Injected<IContentEvents> Events { get; set; }

    protected Injected<IBlobFactory> Blobfactory { get; set; }

    protected Injected<INotifier> Notifier { get; set; }

    protected VisionHandler handler;

    public void Initialize(InitializationEngine context)
    {
        context.InitComplete += Context_InitComplete;
    }

    private void Context_InitComplete(object sender, EventArgs e)
    {
        handler = ServiceLocator.Current.GetInstance<VisionHandler>();
        if (handler.Enabled)
        {
            Events.Service.SavingContent += Service_SavingContent;
        }
    }

    private void Service_SavingContent(object sender, EPiServer.ContentEventArgs e)
    {
        //Only call when an image is being uploaded
        if (e.ContentLink.WorkID == 0 && handler.Enabled && e.Content is ImageData && ((EPiServer.SaveContentEventArgs)e).Action == EPiServer.DataAccess.SaveAction.Publish)
        {
            var img = e.Content as ImageData;

            //Determine if Blob has changed / is new. If not, do nothing. Only act if relevant properties are empty/null
            handler.HandleImage(img);
        }
    }

    public void Uninitialize(InitializationEngine context)
    {
    }
}