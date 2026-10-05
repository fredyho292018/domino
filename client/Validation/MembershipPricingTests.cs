using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
using Newtonsoft.Json.Linq;

static class MembershipPricingTests {
 static int checks;
 static void Need(bool condition,string key){if(!condition)throw new Exception("PRICING_"+key);checks++;}
 static MembershipCatalogDto Catalog(string locale)=>JObject.Parse(File.ReadAllText("client/Validation/MembershipCatalogFixture.json"))[locale].ToObject<MembershipCatalogDto>();
 sealed class Source:IAppMembershipSource {
  public bool IsCurrent=>true;
  public EntitlementSummaryDto Entitlements=>new EntitlementSummaryDto{availability="AVAILABLE",snapshot=new EffectiveEntitlementsDto{membershipPlan="FREE",plan="FREE",status="FREE"}};
  public TrialEligibilityDto TrialEligibility=>new TrialEligibilityDto{eligible=true,state="NOT_STARTED",activationMode="EXPLICIT",periodDays=7,policyVersion=1};
  public event Action Changed {add{}remove{}}
  public Task<MembershipCatalogDto> LoadAsync(string locale,CancellationToken token)=>Task.FromResult(Catalog(locale));
  public void Dispose(){}
 }
 public static async Task Run(){
  var names=new[]{"GOLD","PLATINUM","DIAMOND","FRIENDS_AND_FAMILY"};
  var monthly=new long[]{699,1099,1699,2799};var yearly=new long[]{4999,7999,11999,19900};var percentages=new decimal[]{40,39,41,41};
  foreach(var locale in new[]{"en","es"})using(var controller=new AppMembershipController(new Source())){
   await controller.LoadAsync(locale);
   for(int i=0;i<names.Length;i++)foreach(var period in new[]{"MONTHLY","YEARLY"}){
    controller.SelectMembershipPlan(names[i]);controller.SelectMembershipPeriod(period);
    var offer=MembershipPricePresentation.Resolve(controller.Membership,controller.SelectedMembershipPlan,controller.MembershipBillingPeriod);
    Need(offer!=null&&offer.amountMinorUnits==(period=="MONTHLY"?monthly[i]:yearly[i]),"SWITCH_AMOUNT");
    Need(offer.currencyCode=="USD"&&offer.market=="US","LOCALE_NOT_CURRENCY");
    Need(MembershipPricePresentation.Price(offer,locale).EndsWith(period=="YEARLY"?(locale=="es"?" / año":" / year"):(locale=="es"?" / mes":" / month")),"LOCALIZED_PERIOD");
    Need(!controller.Membership.pricing.purchasesAvailable&&MembershipPricePresentation.Price(offer,locale).StartsWith("$"),"VISIBLE_WITHOUT_PURCHASES");
    Need(controller.CanActivateTrial==(names[i]!="FRIENDS_AND_FAMILY"),"FAMILY_NO_TRIAL");
    var m=MembershipPricePresentation.Resolve(controller.Membership,names[i],"MONTHLY");var y=MembershipPricePresentation.Resolve(controller.Membership,names[i],"YEARLY");
    Need(MembershipPricePresentation.SavingsPercent(m,y)==percentages[i],"DERIVED_SAVINGS");
    Need(MembershipPricePresentation.MonthlyEquivalent(y,locale)!=null&&MembershipPricePresentation.MonthlyEquivalent(m,locale)==null,"EQUIVALENT_YEAR_ONLY");
   }
   controller.SelectMembershipPlan("DIAMOND");controller.SelectMembershipPeriod("YEARLY");
   var diamond=MembershipPricePresentation.Resolve(controller.Membership,"DIAMOND","YEARLY");
   Need(MembershipPricePresentation.Price(diamond,locale)==(locale=="es"?"$119.99 / año":"$119.99 / year"),"TRIAL_SELECTED_POST_PRICE");
   Need(MembershipPricePresentation.Money(0,diamond.currencyCode,locale)=="$0.00","ZERO_TODAY");
   Need(MembershipPricePresentation.MonthlyEquivalent(diamond,locale).Contains("$10.00"),"DERIVED_ROUNDING");
  }
  var catalog=Catalog("en");var pricing=catalog.pricing;catalog.pricing=null;
  Need(MembershipPricePresentation.Resolve(catalog,"GOLD","MONTHLY")==null,"LEGACY_NULL");
  Need(MembershipPricePresentation.Price(null,"es")=="Precio no disponible","NO_ZERO_FALLBACK");catalog.pricing=pricing;
  pricing.offers=pricing.offers.Where(o=>o.planKey!="GOLD").ToArray();Need(MembershipPricePresentation.Resolve(catalog,"GOLD","YEARLY")==null,"MISSING");
  catalog=Catalog("en");var original=catalog.pricing.offers[0];original.active=false;
  Need(MembershipPricePresentation.Resolve(catalog,original.planKey,original.billingPeriod)==null,"INACTIVE");original.active=true;
  original.amountMinorUnits=null;Need(MembershipPricePresentation.Resolve(catalog,original.planKey,original.billingPeriod)==null,"NULL_MONEY");
  original.amountMinorUnits=0;Need(MembershipPricePresentation.Resolve(catalog,original.planKey,original.billingPeriod)==null,"ZERO_MONEY");original.amountMinorUnits=699;
  catalog.pricing.offers=catalog.pricing.offers.Concat(new[]{original}).ToArray();Need(MembershipPricePresentation.Resolve(catalog,original.planKey,original.billingPeriod)==null,"DUPLICATE");
  catalog=Catalog("en");catalog.pricing.market="CA";Need(MembershipPricePresentation.Resolve(catalog,"GOLD","YEARLY")==null,"NO_CROSS_MARKET_FALLBACK");
  catalog=Catalog("en");catalog.pricing.catalogVersion=2;Need(MembershipPricePresentation.Resolve(catalog,"GOLD","YEARLY")==null,"VERSION");
  catalog=Catalog("en");var month=MembershipPricePresentation.Resolve(catalog,"GOLD","MONTHLY");var year=MembershipPricePresentation.Resolve(catalog,"GOLD","YEARLY");
  year.currencyCode="CAD";Need(MembershipPricePresentation.SavingsPercent(month,year)==null,"NO_MIXED_CURRENCY_SAVING");year.currencyCode="USD";year.market="CA";Need(MembershipPricePresentation.SavingsPercent(month,year)==null,"NO_MIXED_MARKET_SAVING");
  Console.WriteLine("MEMBERSHIP_PRICING_CLIENT_CHECKS="+checks+"_PASS");
 }
}
