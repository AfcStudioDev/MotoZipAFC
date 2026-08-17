namespace MotoParts.Domain.Models;

/// <summary>Согласованный набор цен заказа: скидка всегда выведена из прайс-цены.</summary>
public readonly record struct OrderPricing(
    decimal? PriceCost,
    decimal SellCost,
    decimal? Discount,
    decimal? DiscountPercent);

/// <summary>
/// Правило расчёта скидки заказа. Раньше жило только во фронтенде (admin.component.ts), а сервер
/// принимал присланные Discount/DiscountPercent на веру — их можно было прислать любыми, никак
/// не связанными с ценой. Здесь три величины выводятся друг из друга от единственной опорной
/// точки — прайс-цены, поэтому в базу не попадает несогласованная тройка.
/// </summary>
public static class DiscountPolicy
{
    /// <summary>Пользователь задал цену продажи — скидка и процент считаются от неё.</summary>
    public static OrderPricing FromSellCost(decimal? priceCost, decimal sellCost)
    {
        // Без прайс-цены (старые заказы, ручной ввод) скидку считать не от чего.
        if (priceCost is not { } price || price == 0m)
            return new OrderPricing(priceCost, Round2(sellCost), null, null);

        var discount = price - sellCost;
        return new OrderPricing(
            Round2(price),
            Round2(sellCost),
            Round2(discount),
            Round1(discount / price * 100m));
    }

    /// <summary>Пользователь задал скидку в деньгах — цена продажи и процент считаются от неё.</summary>
    public static OrderPricing FromDiscount(decimal? priceCost, decimal discount)
    {
        if (priceCost is not { } price || price == 0m)
            return new OrderPricing(priceCost, 0m, Round2(discount), null);

        return new OrderPricing(
            Round2(price),
            Round2(price - discount),
            Round2(discount),
            Round1(discount / price * 100m));
    }

    /// <summary>Пользователь задал скидку в процентах — цена продажи и сумма скидки считаются от неё.</summary>
    public static OrderPricing FromDiscountPercent(decimal? priceCost, decimal percent)
    {
        if (priceCost is not { } price || price == 0m)
            return new OrderPricing(priceCost, 0m, null, Round1(percent));

        var discount = percent / 100m * price;
        return new OrderPricing(
            Round2(price),
            Round2(price - discount),
            Round2(discount),
            Round1(percent));
    }

    /// <summary>Деньги — до сотых.</summary>
    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Проценты — до десятых, как показывает форма заказа.</summary>
    private static decimal Round1(decimal v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);
}
