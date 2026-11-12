using System.Net;
using System.Net.Http.Json;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace VisorDatosSIG.Mobile;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class FieldTrackingService : Service
{
    private const string Channel = "visordatos-tracking";
    private CancellationTokenSource? tracking;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == "stop")
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }
        var endpoint = intent?.GetStringExtra("endpoint");
        var cookie = intent?.GetStringExtra("cookie");
        var token = intent?.GetStringExtra("token");
        if (endpoint is null || cookie is null || token is null)
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        using var channel = new NotificationChannel(Channel, "Recorrido de campo", NotificationImportance.Low);
        manager.CreateNotificationChannel(channel);
        using var stop = new Intent(this, typeof(FieldTrackingService)).SetAction("stop");
        using var pending = PendingIntent.GetService(this, 0, stop, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        using var action = new Notification.Action.Builder(
            Android.Graphics.Drawables.Icon.CreateWithResource(this, global::Android.Resource.Drawable.IcMenuCloseClearCancel),
            "Detener", pending).Build();
        using var notification = new Notification.Builder(this, Channel)
            .SetContentTitle("VisorDatosSIG · recorrido activo")
            .SetContentText("La ubicación se comparte mientras realizas esta asignación.")
            .SetSmallIcon(ApplicationInfo!.Icon)
            .SetOngoing(true)
            .AddAction(action)
            .Build();
        StartForeground(442, notification);
        tracking?.Cancel();
        var run = new CancellationTokenSource();
        tracking = run;
        _ = SendPositionsAsync(endpoint, cookie, token, run, startId);
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        tracking?.Cancel();
        base.OnDestroy();
    }

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        StopSelf();
        base.OnTaskRemoved(rootIntent);
    }

    private async Task SendPositionsAsync(string endpoint, string cookie, string csrf, CancellationTokenSource run, int startId)
    {
        var ct = run.Token;
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var gps = await Geolocation.Default.GetLocationAsync(
                        new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(15)), ct);
                    if (gps?.Accuracy is > 0 and <= 100)
                    {
                        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
                        {
                            Content = JsonContent.Create(new { lat = gps.Latitude, lng = gps.Longitude, accuracy = gps.Accuracy })
                        };
                        message.Headers.Add("Cookie", cookie);
                        message.Headers.Add("RequestVerificationToken", csrf);
                        using var response = await client.SendAsync(message, ct);
                        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                            || (int)response.StatusCode is >= 300 and < 400) break;
                    }
                }
                catch (PermissionException) { break; }
                catch (FeatureNotEnabledException) { break; }
                catch (FeatureNotSupportedException) { break; }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }
                await Task.Delay(TimeSpan.FromSeconds(12), ct);
            }
        }
        catch (System.OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(tracking, run))
            {
                tracking = null;
                StopSelf(startId);
            }
            run.Dispose();
        }
    }
}
