using MotoParts.Domain.Models;

using Xunit;

namespace MotoParts.Domain.Tests;

/// <summary>
/// Правило скидки заказа. Раньше оно жило только во фронтенде, и сервер принимал присланную
/// тройку (цена, скидка, процент) на веру — прислать можно было любую, никак не связанную
/// с прайсом. Тесты фиксируют, что три величины всегда выводятся друг из друга.
/// </summary>
public class DiscountPolicyTests
{
    [Fact]
    public void FromSellCost_считает_скидку_и_процент_от_прайса()
    {
        var pricing = DiscountPolicy.FromSellCost(priceCost: 1790m, sellCost: 1500m);

        Assert.Equal(1790m, pricing.PriceCost);
        Assert.Equal(1500m, pricing.SellCost);
        Assert.Equal(290m, pricing.Discount);
        // 290 / 1790 * 100 = 16.201… → до десятых
        Assert.Equal(16.2m, pricing.DiscountPercent);
    }

    [Fact]
    public void FromDiscount_считает_цену_продажи_и_процент()
    {
        var pricing = DiscountPolicy.FromDiscount(priceCost: 1790m, discount: 500m);

        Assert.Equal(1290m, pricing.SellCost);
        Assert.Equal(500m, pricing.Discount);
        Assert.Equal(27.9m, pricing.DiscountPercent);
    }

    [Fact]
    public void FromDiscountPercent_считает_скидку_и_цену_продажи()
    {
        var pricing = DiscountPolicy.FromDiscountPercent(priceCost: 1790m, percent: 25m);

        Assert.Equal(447.5m, pricing.Discount);
        Assert.Equal(1342.5m, pricing.SellCost);
        Assert.Equal(25m, pricing.DiscountPercent);
    }

    [Fact]
    public void Три_способа_расчёта_дают_согласованный_результат()
    {
        // Одна и та же сделка, заданная с трёх сторон, должна сойтись в одно и то же.
        var fromSell = DiscountPolicy.FromSellCost(2000m, 1500m);
        var fromDiscount = DiscountPolicy.FromDiscount(2000m, 500m);
        var fromPercent = DiscountPolicy.FromDiscountPercent(2000m, 25m);

        Assert.Equal(fromSell, fromDiscount);
        Assert.Equal(fromSell, fromPercent);
    }

    [Fact]
    public void Продажа_без_скидки_даёт_нули_а_не_null()
    {
        var pricing = DiscountPolicy.FromSellCost(1790m, 1790m);

        Assert.Equal(0m, pricing.Discount);
        Assert.Equal(0m, pricing.DiscountPercent);
    }

    [Fact]
    public void Наценка_выражается_отрицательной_скидкой()
    {
        // Цена продажи выше прайса — скидка уходит в минус, а не обнуляется.
        var pricing = DiscountPolicy.FromSellCost(1000m, 1200m);

        Assert.Equal(-200m, pricing.Discount);
        Assert.Equal(-20m, pricing.DiscountPercent);
    }

    public static TheoryData<decimal?> ПрайсКоторогоНет => new() { null, 0m };

    [Theory]
    [MemberData(nameof(ПрайсКоторогоНет))]
    public void Без_прайса_скидка_не_считается(decimal? priceCost)
    {
        // У старых заказов прайс-цены нет, делить не на что — важно не упасть и не выдумать скидку.
        var pricing = DiscountPolicy.FromSellCost(priceCost, 1500m);

        Assert.Equal(1500m, pricing.SellCost);
        Assert.Null(pricing.Discount);
        Assert.Null(pricing.DiscountPercent);
    }

    [Fact]
    public void Деньги_округляются_до_сотых_а_проценты_до_десятых()
    {
        // 1/3 прайса — заведомо непериодическое деление, проверяем именно округление.
        var pricing = DiscountPolicy.FromDiscountPercent(priceCost: 999.99m, percent: 33.333m);

        Assert.Equal(333.33m, pricing.Discount);
        Assert.Equal(666.66m, pricing.SellCost);
        Assert.Equal(33.3m, pricing.DiscountPercent);
    }
}
