using System;
using System.Globalization;
using System.Linq;
using Domino.Infrastructure.Api;

namespace Domino.UI.AppShell {
 // Amounts are backend data. All arithmetic below is informational display arithmetic.
 public static class MembershipPricePresentation {
  static bool Currency(string code,out int digits) {
   digits=0;if(string.IsNullOrEmpty(code)||code.Length!=3||code!=code.ToUpperInvariant())return false;
   foreach(var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures)) {
    try {if(new RegionInfo(culture.Name).ISOCurrencySymbol==code){digits=culture.NumberFormat.CurrencyDecimalDigits;return digits>=0&&digits<=3;}}catch(ArgumentException){}
   }
   return false;
  }
  public static CommercialOfferDto Resolve(MembershipCatalogDto catalog,string plan,string period) {
   var pricing=catalog?.pricing;
   if(pricing==null||pricing.status!="AVAILABLE"||pricing.authority!="CONFIGURED_COMMERCIAL_OFFER"||pricing.offerVersion.GetValueOrDefault()<=0||pricing.catalogVersion!=catalog.catalogVersion||string.IsNullOrEmpty(pricing.market)||(period!="MONTHLY"&&period!="YEARLY"))return null;
   var matches=(pricing.offers??Array.Empty<CommercialOfferDto>()).Where(o=>o!=null&&o.planKey==plan&&o.billingPeriod==period&&o.market==pricing.market).ToArray();
   if(matches.Length!=1)return null;
   var offer=matches[0];
   return offer.active&&offer.amountMinorUnits.HasValue&&offer.amountMinorUnits.Value>0&&offer.amountMinorUnits.Value<=1000000000000L&&Currency(offer.currencyCode,out _)?offer:null;
  }
  static decimal Scale(int digits){decimal scale=1;for(int i=0;i<digits;i++)scale*=10;return scale;}
  public static string Money(decimal minor,string currency,string locale) {
   if(!Currency(currency,out int digits))return locale=="es"?"Precio no disponible":"Price unavailable";
   var culture=CultureInfo.GetCultureInfo(locale=="es"?"es-US":"en-US");
   // '$' denotes USD for the explicit US offer; other currencies stay unambiguous.
   var prefix=currency=="USD"?"$":currency+" ";
   return prefix+(minor/Scale(digits)).ToString("N"+digits,culture);
  }
  public static string Price(CommercialOfferDto offer,string locale) {
   if(offer==null)return locale=="es"?"Precio no disponible":"Price unavailable";
   return Money(offer.amountMinorUnits.Value,offer.currencyCode,locale)+(offer.billingPeriod=="YEARLY"?(locale=="es"?" / año":" / year"):(locale=="es"?" / mes":" / month"));
  }
  public static string MonthlyEquivalent(CommercialOfferDto offer,string locale) =>offer?.billingPeriod=="YEARLY"?
   (locale=="es"?"Equivale a ":"Equivalent to ")+Money(offer.amountMinorUnits.Value/12m,offer.currencyCode,locale)+(locale=="es"?" / mes":" / month"):null;
  public static decimal? SavingsPercent(CommercialOfferDto monthly,CommercialOfferDto yearly) {
   if(monthly==null||yearly==null||monthly.planKey!=yearly.planKey||monthly.market!=yearly.market||monthly.currencyCode!=yearly.currencyCode||monthly.billingPeriod!="MONTHLY"||yearly.billingPeriod!="YEARLY"||!monthly.active||!yearly.active||monthly.amountMinorUnits.GetValueOrDefault()<=0||yearly.amountMinorUnits.GetValueOrDefault()<=0)return null;
   decimal annual=monthly.amountMinorUnits.Value*12m;var saving=annual-yearly.amountMinorUnits.Value;
   return saving>0?decimal.Round(saving*100m/annual,0,MidpointRounding.AwayFromZero):(decimal?)null;
  }
  public static string Savings(CommercialOfferDto monthly,CommercialOfferDto yearly,string locale) {
   var percent=SavingsPercent(monthly,yearly);if(!percent.HasValue)return null;
   return (locale=="es"?"Ahorra ":"Save ")+Money(monthly.amountMinorUnits.Value*12m-yearly.amountMinorUnits.Value,yearly.currencyCode,locale)+" · "+percent.Value.ToString("0",CultureInfo.InvariantCulture)+"%";
  }
 }
}
