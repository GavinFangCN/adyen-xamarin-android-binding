using System;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Com.Adyen.Checkout.Base.Model.Payments.Request;
using Com.Adyen.Checkout.Card;
using DemoAppV3.Model;
using Java.Lang;

namespace DemoAppV3
{
	public static class Config
	{
		public const string ServerUrl = "https://checkout-test.adyen.com/checkout/v49/";
		//Insert your test api key, public key and merchant account here
		public const string ApiKey = "AQEkhmfuXNWTK0Qc+iScm3c5gPaUXZsvUirrDFstiyH7TS010QqiEMFdWw2+5HzctViMSCJMYAc=-XDnDcnyDYt1rj0MsgTN3wuccn6fI5rOR/66rCD4+dzI=-Qn6xQddxCUvfAZV2";
		public const string MerchantAccount = "LisaGroupECOM";
		public const string PublicKey = "10001|B67C096EEEA1954085DE98BA779EB9CBBFF0522E9609ED907991FDAE95807B8A8C20E7CF15B6B8302FA3C08C436530CEB0E432EFF9B73A168A5AA0200DDFF8E207AC8D28F88934735922FE6C8D7039674760CA01A688D28E47B59F133BD796681AB7A33719D61FE0AB55C098CD554C281EC7C3BB8462AB233257FD102DB40E82899C8FFC29BAF97348B3821BAE9C6ECF71ABB5334AFC8357361719050FB40C12AD1BB4D3D88431E49D604715C463C3BC619A6971804E2769A4104F2B62B53BCEEA8F07D42F8DBDD5BB6E583EBFD5BA429A6DFC1E79870C491E1A7896C39F16F8166CFC685C4E42CFB191CFB89C3D4BB4EF9590C4D387090CDAE9145AAEF5E887";

		//These can be changed to your liking
		public const string CountryCode = "sv";
		public const string ShopperLocale = "en_GB";
		public const string Currency = "EUR";
	}

	[Activity(Name = "com.companyname.demoappv3.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true, LaunchMode = LaunchMode.SingleTask)]
	[IntentFilter(
		new[] { Intent.ActionView },
		Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
		DataScheme = "adyencheckout",
		DataHost = "com.companyname.demoappv3")]
	public class MainActivity : AppCompatActivity
	{
		RelativeLayout layout;
		TextView textViewLoading;
		FloatingActionButton fab;

		CardPaymentMethod paymentMethod;
		private decimal amount = 10;
		Com.Adyen.Checkout.Redirect.RedirectComponent redirectComponent;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			Xamarin.Essentials.Platform.Init(this, savedInstanceState);
			SetContentView(Resource.Layout.activity_main);

			Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
			if (SupportActionBar == null)
				SetSupportActionBar(toolbar);

			fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
			fab.Enabled = false;
			fab.Click += PayButton_Click;

			layout = FindViewById<RelativeLayout>(Resource.Id.relativeLayout1);
			textViewLoading = FindViewById<TextView>(Resource.Id.textViewLoading);

			InitPaymentForm();
		}

		protected override void OnResume()
		{
			base.OnResume();

			var intent = Intent;

			if (intent?.Data?.ToString()?.StartsWith(Com.Adyen.Checkout.Redirect.RedirectUtil.RedirectResultScheme) == true)
			{
				redirectComponent?.HandleRedirectResponse(intent.Data);
			}

		}

		public override bool OnCreateOptionsMenu(IMenu menu)
		{
			MenuInflater.Inflate(Resource.Menu.menu_main, menu);
			return true;
		}

		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			int id = item.ItemId;
			if (id == Resource.Id.action_settings)
			{
				return true;
			}

			return base.OnOptionsItemSelected(item);
		}

		private void InitPaymentForm()
		{
			Task.Run(async () =>
			{
				var paymentMethods = await DemoBackend.GetPaymentMethods(amount);
				var cardPaymentMethod = paymentMethods.FirstOrDefault(x => x.Type == "scheme");
				if (cardPaymentMethod == null)
				{
					RunOnUiThread(() => Snackbar.Make(layout, "Credit card payments not available!", Snackbar.LengthLong).Show());
					return;
				}

				var cardConfigurationBuilder = new CardConfiguration.Builder(this, Config.PublicKey);
				cardConfigurationBuilder.SetEnvironment(Com.Adyen.Checkout.Core.Api.Environment.Europe);
				var cardConfiguration = cardConfigurationBuilder.Build();

				RunOnUiThread(() =>
				{
					try
					{

						var cardComponent = CardComponent.Provider.Get(this, cardPaymentMethod, cardConfiguration) as CardComponent;

						var cardView = new CardView(this);// { Id = View.GenerateViewId() };
						var cardLayout = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
						cardLayout.AddRule(LayoutRules.AlignParentTop);
						cardView.Attach(cardComponent, this);
						layout.AddView(cardView, cardLayout);
						textViewLoading.Visibility = ViewStates.Gone;

						cardComponent.Observe(this, new PaymentComponentObserver
						{
							Changed = state =>
							{
								var paymentMethod = state.Data.PaymentMethod as CardPaymentMethod;

								Android.Util.Log.Debug("ObserverImpl", $"Onchanged -- Valid: {state.IsValid}, {paymentMethod.Type}");

								if (state.IsValid)
								{
									fab.Enabled = true;
									this.paymentMethod = paymentMethod;
								}
								else
									fab.Enabled = false;
							}
						});
					}
					catch (System.Exception ex)
					{
						Android.Util.Log.Error("MainActivity", Throwable.FromException(ex), ex.Message);
					}
				});


			});
		}

		private void PayButton_Click(object sender, EventArgs e)
		{
			Task.Run(async () =>
			{
				var paymentResult = await DemoBackend.ExecutePayment(paymentMethod, "adyencheckout://com.companyname.demoappv3", amount);

				if (paymentResult.ResultCode == "RedirectShopper")
				{
					HandleRedirect(paymentResult);
					return;
				}

				ShowPaymentStatus(paymentResult);
			});
		}

		private void ShowPaymentStatus(PaymentsResponse paymentResult)
		{
			var message = paymentResult.ResultCode;

			if (paymentResult.ResultCode == "Authorised")
				message += $" ({paymentResult.PspReference})";
			else if (!string.IsNullOrWhiteSpace(paymentResult.RefusalReasonCode))
				message += $" - {paymentResult.RefusalReason} ({paymentResult.RefusalReasonCode})";
			RunOnUiThread(() => Snackbar.Make(layout, message, Snackbar.LengthLong).Show());
		}

		void HandleRedirect(PaymentsResponse paymentResult)
		{
			redirectComponent = Com.Adyen.Checkout.Redirect.RedirectComponent.Provider.Get(this) as Com.Adyen.Checkout.Redirect.RedirectComponent;
			redirectComponent.Observe(this, new RedirectComponentObserver {
				Changed = data =>
				{
					var paymentData = data.Details.GetString("paymentData");
					var details = data.Details.GetJSONObject("details");
					var paRes = details.GetString("PaRes");
					var md = details.GetString("MD");

					Android.Util.Log.Debug("ObserverImpl", $"paymentData: {paymentData}");
					Task.Run(async () =>
					{
						var result = await DemoBackend.GetPaymentDetails(paRes, md, paymentData);
						ShowPaymentStatus(result);
					});
				}
			});

			redirectComponent.HandleAction(this, paymentResult.Action.AsRedirectAction());
		}

		public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
		{
			Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

			base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
		}

		protected override void OnNewIntent(Intent intent)
		{
			base.OnNewIntent(intent);
			Intent = intent;
		}

		public class PaymentComponentObserver : Java.Lang.Object, Android.Arch.Lifecycle.IObserver
		{
			public Action<Com.Adyen.Checkout.Base.PaymentComponentState> Changed;

			public void OnChanged(Java.Lang.Object p0)
			{
				if (p0 is Com.Adyen.Checkout.Base.PaymentComponentState state)
					Changed?.Invoke(state);
			}
		}

		public class RedirectComponentObserver : Java.Lang.Object, Android.Arch.Lifecycle.IObserver
		{
			public Action<Com.Adyen.Checkout.Base.ActionComponentData> Changed;

			public void OnChanged(Java.Lang.Object p0)
			{
				if (p0 is Com.Adyen.Checkout.Base.ActionComponentData data)
					Changed?.Invoke(data);
			}
		}
	}
}

