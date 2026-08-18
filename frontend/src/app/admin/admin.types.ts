// Типы админ-панели. Вынесены из admin.component.ts: они описывают контракт между
// компонентом, конфигурацией таблиц и подкомпонентами, и нужны им всем.

export interface ZipPhotoRow {
  id: number;
  fileName: string;
  isMain: boolean;
}

export interface FieldDef {
  key: string;
  label: string;
  /**
   * zip-picker — выбор запчасти в два шага: сначала парт-номер, затем конкретный
   * экземпляр. Нужен потому, что одно наименование встречается у разных парт-номеров
   * (например, «Масляный фильтр» и у Honda, и у Yamaha), и по одному названию
   * невозможно понять, какая именно деталь выбирается.
   */
  /** file-link — ссылка на файл, загруженный не через эту форму (например, чек оплаты клиентом); только просмотр. */
  type: 'text' | 'number' | 'checkbox' | 'select' | 'date' | 'zip-picker' | 'catalog-picker' | 'group-picker' | 'stock' | 'file-link';
  required?: boolean;
  refTable?: string;
  refLabelKey?: string;
  /** Поле принадлежит каталожной позиции (PartNumbers), а не самой записи — сохраняется отдельным запросом. */
  partNumberOwned?: boolean;
  /**
   * Для catalog-picker: по какому свойству PartNumber ищутся подсказки (парт-номер
   * или наименование) — оба поля ищут по одному и тому же справочнику part-numbers,
   * и выбор в любом из них подставляет оба значения (см. applyCatalogSuggestion).
   */
  catalogRole?: 'partNum' | 'name';
  /** Кнопка «+» рядом с полем, открывающая модалку быстрого добавления записи в этот справочник. */
  quickAdd?: 'part-number' | 'group' | 'incomemoto';
  /**
   * Для select: показывать в выпадающем списке только записи справочника, у которых
   * userId совпадает с выбранным в форме form['userId']. Если у покупателя нет ни
   * одного своего адреса — показывается полный список, чтобы поле не оставалось пустым.
   */
  filterByUserId?: boolean;
  /** Поле выводится только для чтения — значение проставляет код, а не пользователь. */
  readonly?: boolean;
  /** Для file-link: подпапка в wwwroot, где отдаются файлы (например, "Receipts"). */
  fileFolder?: string;
}

/** Что именно создаём в модалке быстрого добавления и как это применить к форме после сохранения. */
export type QuickAddKind = 'part-number' | 'group' | 'incomemoto';

/** Поле строки поиска. Только содержательные колонки — без Id и внешних ключей. */
export interface SearchFieldDef {
  key: string;
  label: string;
}

export interface TableDef {
  endpoint: string;
  title: string;
  fields: FieldDef[];
  searchFields?: SearchFieldDef[];
}

// Решает проблему TS4111 (noPropertyAccessFromIndexSignature)
export interface DynamicRow {
  id: any;
  [key: string]: any;
}

/**
 * Одна строка применимости парт-номера к модели, выбранная/введённая в форме,
 * но ещё не обязательно сохранённая. applicabilityId задан только для уже
 * существующих связей (используется, чтобы отличить их от новых при сохранении).
 * modelId/markId заданы, только если модель/марка выбраны из существующего справочника —
 * иначе при сохранении их создаст syncApplicability по названию.
 */
export interface StagedModelLink {
  applicabilityId?: number;
  modelId?: number;
  markId?: number;
  markName: string;
  modelName: string;
}

/** То же самое для серий — независимая от моделей связка (см. syncApplicability). */
export interface StagedSeriesLink {
  applicabilityId?: number;
  seriesId?: string;
  seriesName: string;
}

/** Расхождение цены с уже заведёнными запчастями того же парт-номера. */
export interface PriceConflict {
  partNumId: number;
  partNum: string;
  name: string;
  /** Цена, стоящая сейчас у запчастей в базе. */
  existingCost: number;
  /** Цена, введённая пользователем. */
  newCost: number;
  /** Сколько запчастей с этим парт-номером уже заведено. */
  count: number;
}
