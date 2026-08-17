using System.ComponentModel;
using System.Reflection;

namespace MotoParts.Domain.Models
{
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            Type type = value.GetType();
            string name = Enum.GetName(type, value);

            if (name != null)
            {
                FieldInfo field = type.GetField(name);
                if (field != null)
                {
                    // Ищем атрибут Description
                    if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attr)
                    {
                        return attr.Description;
                    }
                }
            }

            // Если атрибута нет, возвращаем просто имя элемента
            return value.ToString();
        }

        // Метод 2: Работает напрямую для числа (int), если вы знаете тип Enum
        public static string GetDescription<T>(this int value) where T : Enum
        {
            // Проверяем, существует ли такое число в перечислении
            if (Enum.IsDefined(typeof(T), value))
            {
                // Приводим число к типу Enum и вызываем первый метод
                Enum enumValue = (Enum)Enum.ToObject(typeof(T), value);
                return enumValue.GetDescription();
            }

            return "Неизвестный статус";
        }
    }
}
