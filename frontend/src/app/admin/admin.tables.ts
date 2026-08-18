import { TableDef } from './admin.types';

/**
 * Описание таблиц админ-панели: какие поля показывать в форме и по чему искать.
 * Это статические данные, а не состояние компонента, — держать их в admin.component.ts
 * (175 строк посреди логики) было неудобно и мешало читать сам компонент.
 */
export const ADMIN_TABLES: TableDef[] = [
  {
    endpoint: 'marks',
    title: 'Марки мотоциклов',
    fields: [
      { key: 'mark', label: 'Марка', type: 'text', required: true }
    ],
    searchFields: [{ key: 'mark', label: 'Марка' }]
  },
  {
    endpoint: 'models',
    title: 'Модели',
    fields: [
      { key: 'markId', label: 'Марка', type: 'select', refTable: 'marks', refLabelKey: 'mark', required: true },
      { key: 'model', label: 'Модель', type: 'text', required: true }
    ],
    searchFields: [{ key: 'model', label: 'Модель' }]
  },
  {
    // Серия не привязана к конкретной модели — независимая классификация
    // (см. PartNumberSeriesApplicability ниже).
    endpoint: 'series',
    title: 'Серии',
    fields: [
      { key: 'seriesName', label: 'Название серии', type: 'text', required: true }
    ],
    searchFields: [{ key: 'seriesName', label: 'Название серии' }]
  },
  {
    endpoint: 'groups',
    title: 'Группы запчастей',
    fields: [
      { key: 'groupName', label: 'Название группы', type: 'text', required: true }
    ],
    searchFields: [{ key: 'groupName', label: 'Название группы' }]
  },
  {
    endpoint: 'part-numbers',
    title: 'Парт-номера',
    fields: [
      { key: 'partNum', label: 'Парт-номер', type: 'text', required: true },
      { key: 'name', label: 'Наименование', type: 'text', required: true },
      { key: 'groupId', label: 'Группа запчастей', type: 'select', refTable: 'groups', refLabelKey: 'groupName' }
    ],
    searchFields: [
      { key: 'partNum', label: 'Парт-номер' },
      { key: 'name', label: 'Наименование' }
    ]
  },
  {
    // Применимость: одна каталожная позиция подходит к нескольким моделям.
    endpoint: 'applicability',
    title: 'Применимость к моделям',
    fields: [
      { key: 'partNumId', label: 'Парт-номер', type: 'select', refTable: 'part-numbers', refLabelKey: 'partNum', required: true },
      { key: 'modelId', label: 'Модель', type: 'select', refTable: 'models', refLabelKey: 'model', required: true }
    ],
    searchFields: [
      { key: 'partNum', label: 'Парт-номер' },
      { key: 'model', label: 'Модель' }
    ]
  },
  {
    // Независимая от моделей привязка: у одного парт-номера может быть
    // любое число моделей и любое число серий одновременно (см. server-side
    // PartNumberSeriesApplicability — отдельная таблица со своей уникальностью).
    endpoint: 'series-applicability',
    title: 'Применимость к сериям',
    fields: [
      { key: 'partNumId', label: 'Парт-номер', type: 'select', refTable: 'part-numbers', refLabelKey: 'partNum', required: true },
      { key: 'seriesId', label: 'Серия', type: 'select', refTable: 'series', refLabelKey: 'seriesName', required: true }
    ],
    searchFields: [
      { key: 'partNum', label: 'Парт-номер' },
      { key: 'series', label: 'Серия' }
    ]
  },
  {
    endpoint: 'incomemotos',
    title: 'Доноры',
    fields: [
      { key: 'description', label: 'Описание', type: 'text', required: true },
      { key: 'userId', label: 'Поставщик', type: 'select', refTable: 'users', refLabelKey: 'fio' }
    ],
    searchFields: [{ key: 'description', label: 'Описание' }]
  },
  {
    endpoint: 'zip',
    title: 'Запчасти (Приход)',
    fields: [
      // Парт-номер и наименование вводятся вручную с подсказками из справочника
      // part-numbers; выбор подсказки в любом из полей заполняет оба (см. applyCatalogSuggestion).
      // Группа принадлежит парт-номеру: подставляется при выборе и сохраняется
      // отдельным запросом в PartNumbers.
      { key: 'partNum', label: 'Парт-номер', type: 'catalog-picker', catalogRole: 'partNum', required: true, quickAdd: 'part-number' },
      { key: 'name', label: 'Наименование', type: 'catalog-picker', catalogRole: 'name', required: true, partNumberOwned: true, quickAdd: 'part-number' },
      { key: 'groupId', label: 'Группа запчастей', type: 'group-picker', refTable: 'groups', refLabelKey: 'groupName', partNumberOwned: true, quickAdd: 'group' },
      { key: 'incomeCost', label: 'Закупочная цена', type: 'number', required: true },
      { key: 'sellCost', label: 'Цена продажи', type: 'number' },
      // Число редактируется только при добавлении новой партии; при редактировании
      // существующей запчасти поле показывает сумму по парт-номеру и заблокировано —
      // см. type: 'stock'.
      { key: 'countStored', label: 'Количество в приходе', type: 'stock', required: true },
      { key: 'year', label: 'Год выпуска (YYYY)', type: 'number' },
      { key: 'incomeDate', label: 'Дата поступления', type: 'date' },
      { key: 'incomeMotoId', label: 'Донор (IncomeMoto)', type: 'select', refTable: 'incomemotos', refLabelKey: 'description', required: true, quickAdd: 'incomemoto' },
      { key: 'comment', label: 'Комментарий', type: 'text' }
    ],
    searchFields: [
      { key: 'partNum', label: 'Парт-номер' },
      { key: 'name', label: 'Наименование' }
    ]
  },
  {
    endpoint: 'users',
    title: 'Пользователи',
    fields: [
      { key: 'email', label: 'Email', type: 'text', required: true },
      { key: 'fio', label: 'ФИО', type: 'text', required: true },
      { key: 'phoneNumber', label: 'Телефон', type: 'text' },
      { key: 'isAdmin', label: 'Администратор', type: 'checkbox' },
      { key: 'isRegistrar', label: 'Регистратор', type: 'checkbox' },
      { key: 'isSender', label: 'Отправитель', type: 'checkbox' },
      { key: 'password', label: 'Новый пароль', type: 'text' }
    ],
    searchFields: [
      { key: 'fio', label: 'ФИО' },
      { key: 'email', label: 'Email' },
      { key: 'phoneNumber', label: 'Телефон' }
    ]
  },
  {
    endpoint: 'addressess',
    title: 'Адреса доставки',
    fields: [
      { key: 'address', label: 'Адрес', type: 'text', required: true },
      { key: 'postCode', label: 'Почтовый индекс', type: 'text' },
      { key: 'userId', label: 'Покупатель', type: 'select', refTable: 'users', refLabelKey: 'fio' }
    ],
    searchFields: [
      { key: 'address', label: 'Адрес' },
      { key: 'postCode', label: 'Индекс' }
    ]
  },
  {
    endpoint: 'orders',
    title: 'Заказы',
    fields: [
      { key: 'zipId', label: 'Запчасть', type: 'zip-picker', required: true },
      { key: 'countOrdered', label: 'Кол-во', type: 'number', required: true },
      // Онлайн-оплата (ЮKassa) сейчас отключена — админ подтверждает оплату вручную.
      // От этого флага зависит, что можно сделать с заказом в «Отправлениях» (см. SenderController).
      { key: 'isPaid', label: 'Оплачено', type: 'checkbox' },
      { key: 'userId', label: 'Покупатель', type: 'select', refTable: 'users', refLabelKey: 'fio', required: true },
      { key: 'addressId', label: 'Адрес доставки', type: 'select', refTable: 'addressess', refLabelKey: 'address', required: true, filterByUserId: true },
      // Цена продажи, Скидка и Скидка в % — три связанных поля: правка любого из них
      // пересчитывает два других от прайс-цены (см. onNumberFieldChange/recalcOrderPricing).
      { key: 'sellCost', label: 'Цена продажи', type: 'number', required: true },
      // Прайс-цена подставляется вместе с ценой продажи (см. applyZipSellCost) и дальше
      // не редактируется вручную — это единственная опорная точка, от которой считаются скидки.
      { key: 'priceCost', label: 'Прайс цена', type: 'number', readonly: true },
      { key: 'discount', label: 'Скидка', type: 'number' },
      { key: 'discountPercent', label: 'Скидка в %', type: 'number' },
      { key: 'orderDateTime', label: 'Дата заказа', type: 'date' },
      { key: 'orderNumber', label: 'Комментарий заказа', type: 'text' },
      // Клиент выбирает при оформлении в каталоге; для заказов из админки поле обычно пустое.
      { key: 'deliveryCompany', label: 'Компания доставки', type: 'text' },
      { key: 'deliveryComment', label: 'Комментарий к доставке', type: 'text' }
    ],
    searchFields: [
      { key: 'orderNumber', label: 'Комментарий заказа' },
      { key: 'partNum', label: 'Парт-номер' },
      // Бэкенд отдаёт наименование в поле zipName; прежний ключ nomenclatureName
      // не существовал в ответе, из-за чего поиск по запчасти ничего не находил.
      { key: 'zipName', label: 'Наименование' },
      // Поиск по userId (сырому id) был бесполезен — искать приходилось по числу.
      // Бэкенд теперь отдаёт userFio отдельным полем специально для поиска по имени.
      { key: 'userFio', label: 'Покупатель' }
    ]
  }
];
