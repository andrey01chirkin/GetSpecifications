# MultiCAD.NET API — Справочник для nanoCAD 26.0

## 1. Общая информация

**Платформа:** nanoCAD 26.0  
**API:** MultiCAD.NET (.NET 6.0, x64)  
**Тип проекта VS:** Class Library, .NET 6.0, платформа x64  

### Зависимости (Copy Local = No)
Путь: `C:\Program Files\Nanosoft\nanoCADx64\bin_nPlat\`  
- `imapimgd.dll`
- `mapimgd.dll`
- `mapibasetypes.dll`

### Загрузка плагина в nanoCAD
- Команда `APPLOAD`, или
- Меню: Сервис → Приложения → Загрузка .NET приложения

---

## 1b. Два мира API — MultiCAD и HostMgd

В nanoCAD сосуществуют **два слоя API**, которые используются одновременно:

| | MultiCAD (`Multicad.*`) | HostMgd (`HostMgd.*`) |
|---|---|---|
| Назначение | Работа с объектами чертежа | Ввод данных и вывод сообщений |
| Откуда | Родной API nanoCAD | Совместимый слой (похож на AutoCAD API) |
| Примеры | `McObjectManager`, `McParametricObject` | `Editor.WriteMessage`, `PromptEntityOptions` |

На практике — MultiCAD для работы с объектами, HostMgd для взаимодействия с пользователем:

```csharp
// MultiCAD — выбор объекта на чертеже
McObjectId id = McObjectManager.SelectObject("\nВыберите объект: ");

// HostMgd — вывод результата в командную строку
Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nГотово");
```

---

## 1c. Архитектурная концепция: две природы объекта

Каждый объект чертежа существует в **двух ипостасях** одновременно.

**Системная часть** (есть у любого объекта — линии, текста, болта):
- хранение в файле `.dwg`
- слой, цвет, видимость
- `Update()`, `Erase()`, `Clone()`

**Бизнес-часть** (уникальна для конкретного типа):
- логика и параметры объекта
- `Origin`, `Name`, `Change()`
- специфично для болта/сваи/символа

Связь между ними — **композиция** (не наследование). Бизнес-часть **держит ссылки** на системную:

```
McParametricObject  (бизнес-часть — «болт»)
├── .DbObject  ──→  McDbObject  (системная часть — «паспорт»)
└── .DbEntity  ──→  McDbEntity  (системная часть — «паспорт с геометрией»)
```

Это объясняет, почему `DbObject` и `DbEntity` не видны в списке **собственных** свойств `McParametricObject` — они унаследованы от `McEntity` (см. раздел 9a).

**Когда что использовать:**

```csharp
// Бизнес-часть — что объект из себя представляет:
paramObj.Origin        // точка вставки
paramObj.Name          // "Болт M8x60"
paramObj.Change(...)   // изменить параметр

// Системная часть через DbEntity — как выглядит:
paramObj.DbEntity.Layer   // слой
paramObj.DbEntity.Color   // цвет
paramObj.DbEntity.Transform(matrix)  // трансформация

// Системная часть через DbObject — запись в БД:
paramObj.DbObject.Update()  // применить изменения
paramObj.DbObject.Erase()   // удалить
paramObj.DbObject.Clone()   // клонировать
```

---

## 2. Пространства имён

```csharp
using Multicad;                                   // McObjectId, McObject
using Multicad.AplicationServices;               // McContext (ВАЖНО: одна 'p' — AplicationServices)
using Multicad.CustomObjectBase;                 // McCustomBase
using Multicad.DatabaseServices;                 // McObjectManager, McDocumentsManager, McDbObject, McDbEntity
using Multicad.DatabaseServices.StandardObjects; // DbPolyline, Polyline3d
using Multicad.Geometry;                         // Point3d, Vector3d, Matrix3d, Plane3d
using Multicad.Objects;                          // McParametricObject
using Multicad.Runtime;                          // CommandMethod, CommandFlags
// Совместимый слой HostMgd:
using HostMgd.ApplicationServices;              // Application → Editor.WriteMessage
using HostMgd.EditorInput;                      // PromptEntityOptions, PromptStatus
```

---

## 3. Структура команды

```csharp
[CommandMethod("CommandName", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
public void CommandName()
{
    // тело команды
}
```

**CommandFlags:**
- `NoCheck | NoPrefix` — **обязательная комбинация** для регистрации команды в MultiCAD.NET. Без неё команда не появляется в nanoCAD после `NETLOAD`
- `NoCheck` — в AutoCAD.NET означает «не проверять активный документ»; в MultiCAD.NET является частью механизма регистрации команды
- `NoPrefix` — имя команды без префикса namespace (иначе команда регистрируется как `NAMESPACE_COMMANDNAME`)

> ⚠️ `MdiActiveDocument` равен `null` только при автозагрузке сборки через секцию `[NetModules]` (до открытия чертежа). При вызове команды пользователем вручную — всегда не `null`.

---

## 4. Иерархия ключевых классов

```
MarshalByRefObject  (.NET)
│
└── McObject  (Multicad) : IMcSerializable, IMcStreamSerializable, IDisposable
      │
      ├── McEntity  (Multicad.DatabaseServices)
      │   ├── McCustomBase  (Multicad.CustomObjectBase) ← кастомные объекты разработчика
      │   └── McCdEntity    (Multicad.DatabaseServices) : IMcOverlay
      │       └── McParametricObject (Multicad.Objects) : I3dQueryable  ← СПДС объекты
      │
      └── McPropertySource  (Multicad.DatabaseServices)  ← CustomProperties, ObjectProperties
            └── McDbObject  (Multicad.DatabaseServices) : ICloneable
                  └── McDbEntity  (Multicad.DatabaseServices)
```

⚠️ **McParametricObject НЕ наследует McCustomBase** — это разные ветки!  
⚠️ **`is McCustomBase` для СПДС объекта вернёт `false`** — использовать `is McParametricObject`.

### McPropertySource (базовый для McDbObject)
- `CustomProperties` — пользовательские свойства (key-value, произвольные данные)
- `ObjectProperties` — свойства объекта (параметры скрипта СПДС — здесь, тип `McProperty`)
- `SystemProperties` — системные свойства
- `GetProperties()` — возвращает источник свойств

---

## 5. McObjectId

**Namespace:** `Multicad`  
**Класс:** `public class McObjectId : IComparable<McObjectId>`

| Член | Описание |
|------|----------|
| `IsNull` | Проверка что ID пустой |
| `McObjectId.Null` | Статическое пустое значение |
| `id.GetObject()` | Получить объект по ID → `McObject` |

**Конструкторы:**

| Конструктор | Когда использовать |
|-------------|-------------------|
| `McObjectId(IntPtr ptr)` | Мост из HostMgd → MultiCAD. Используется когда объект выбран через `PromptEntityResult`: `new McObjectId(res.ObjectId.OldIdPtr)` |
| `McObjectId(Guid guid)` | Когда Guid объекта известен заранее (сохранён из `McDbObject.Guid`). Позволяет восстановить ID без выбора пользователем |

```csharp
// HostMgd → MultiCAD (выбор пользователем):
PromptEntityResult res = Ed.GetEntity(opts);
McObjectId id = new McObjectId(res.ObjectId.OldIdPtr);

// По сохранённому Guid:
Guid saved = myObj.DbObject.Guid;
// ... позже ...
McObjectId id = new McObjectId(saved);
McObject obj = McObjectManager.GetObject(id);
```

---

## 6. McObject

**Namespace:** `Multicad`  
Базовый класс для всех объектов.

| Член | Описание |
|------|----------|
| `ID` | Идентификатор объекта |
| `ClassID` | Идентификатор класса |
| `Cast(...)` | Приведение типа |
| `IsKindOf(...)` | Проверка типа |

---

## 7. McDbObject

**Namespace:** `Multicad.DatabaseServices`

| Член | Тип | Описание |
|------|-----|----------|
| `Clone()` | `object` | Клонировать объект, зануляет внешние ссылки |
| `Clone(bool saveLinkID)` | `object` | Клонировать, сохраняя ID ссылок |
| `AddToCurrentDocument()` | void | Добавить в текущий документ |
| `Update()` | void | Обновить объект |
| `Erase()` | void | Удалить объект |
| `Entity` | `McObject` | Бизнес-объект (McCustomBase/McParametricObject) |
| `Document` | `McDocument` | Документ-владелец |
| `DocumentID` | | ID документа |
| `Guid` | | Уникальный идентификатор на чертеже |
| `Params` | `Parameters` | ⚠️ Настройки приложения (БД, версия и т.д.) — НЕ параметры скрипта СПДС! |
| `CustomProperties` | (McPropertySource) | Пользовательские свойства |
| `ObjectProperties` | (McPropertySource) | Свойства объекта |
| `SystemProperties` | (McPropertySource) | Системные свойства |
| `AddReactor(idTo)` | void | Добавить реактор |
| `IsModified` | bool | Изменён и не обновлён |
| `IsInEdit` | bool | Редактируется в данный момент |

---

## 8. McDbEntity

**Namespace:** `Multicad.DatabaseServices`  
Наследует `McDbObject`.

### Методы
| Метод | Описание |
|-------|----------|
| `Transform(Matrix3d tfm)` | Трансформация объекта (**не TransformBy!**) |
| `Explode()` | Массив геометрических примитивов |
| `GetPlane()` | Плоскость объекта (если плоский) |
| `GetNearestPoint(pnt)` | Ближайшая точка на геометрии |
| `Highlight(...)` | Подсветка |
| `MatchProperties(obj)` | Копировать свойства с другого объекта |
| `AddField(...)` | Добавить поле |
| `GetFieldValue(name)` | Получить значение поля |

### Свойства
| Свойство | Описание |
|----------|----------|
| `BoundingBox` | Ограничивающий блок |
| `Color` | Цвет |
| `Layer` | Слой |
| `LineType` | Тип линии |
| `LineWeight` | Вес линии |
| `OCS` | Матрица трансформации ECS→WCS |
| `Visibility` | Видимость (1/0) |
| `Scale` | Масштаб оформления |
| `DScale` | Масштаб модели |
| `ChildrenIds` | Дочерние объекты |
| `ParentId` | ID родителя |
| `ObjectProperties` | Коллекция `McProperty` — параметры скрипта СПДС |

---

## 9. McCustomBase

**Namespace:** `Multicad.CustomObjectBase`  
Базовый класс всех пользовательских объектов.  
Наследует: `McEntity` → `McObject`  
⚠️ **McCustomBase и McParametricObject — разные ветки, не связаны между собой!**

| Член | Описание |
|------|----------|
| `DbEntity` | Системная часть → `McDbEntity` |
| `DbObject` | Системная часть → `McDbObject` |
| `PlaceObject()` | Разместить объект |
| `OnDraw(...)` | Переопределить для отрисовки |
| `OnTransform(tfm)` | Вызывается при трансформации |

---

## 9a. McEntity

**Namespace:** `Multicad.DatabaseServices`  
Базовый класс для всех «видимых» объектов чертежа.  
Наследует: `McObject`

⚠️ **Именно здесь определены свойства `DbObject` и `DbEntity`** — поэтому они доступны и у `McCustomBase`, и у `McParametricObject`, хотя в их собственных списках свойств не видны.

| Свойство | Тип | Описание |
|----------|-----|----------|
| `DbObject` | `McDbObject` | Ссылка на системную часть (хранение в БД) |
| `DbEntity` | `McDbEntity` | Ссылка на системную часть с геометрией |

```csharp
// DbObject и DbEntity определены в McEntity,
// поэтому работают для ЛЮБОГО наследника:
paramObj.DbObject.Update()      // McParametricObject → McEntity.DbObject
customObj.DbEntity.Layer        // McCustomBase → McEntity.DbEntity
dbPoly.DbEntity.Color           // DbPolyline → McEntity.DbEntity
```

---

## 9b. McCdEntity

**Namespace:** `Multicad.DatabaseServices`  
Базовый класс для `McParametricObject`. Отдельная ветка от `McCustomBase`.  
Наследует: `McEntity` → `McObject`

Является родителем для параметрических объектов СПДС.  
Конкретный состав свойств и методов — определяется в `McParametricObject`.

---

## 10. McParametricObject

**Namespace:** `Multicad.Objects`  
Тип всех параметрических объектов СПДС (болты, арматура, условные обозначения и т.д.).  
Наследует: `McCdEntity` → `McEntity` → `McObject`  
⚠️ **НЕ наследует McCustomBase!** Это отдельная ветка иерархии.

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Origin` | `Point3d` | **Точка вставки** (положение первого штриха) |
| `Direction` | `Vector3d` | Направление объекта (WCS) |
| `DirectionOCS` | `Vector3d` | Направление (OCS) |
| `PlaneVector` | `Vector3d` | Вектор плоскости |
| `OriginOCS` | `Point3d` | Точка вставки в OCS |
| `Name` | `string` | Конкретное наименование (напр. "Болт M8x60 ГОСТ 1225") |
| `CommonName` | `string` | Общее наименование из БД |
| `ClassName` | `string` | Переменная strTheName из скрипта |
| `ClassType` | `string` | Переменная strTheType из скрипта |
| `ClassSubType` | `string` | Переменная strTheSubType из скрипта |
| `ElementID` | | ID объекта в базе данных СПДС |
| `Implementation` | `string` | Имя исполнения |
| `LocalizedImplementation` | `string` | Локализованное имя исполнения |
| `OwnerID` | | ID владельца |
| `IsAbstract` | `bool` | Абстрактный объект |
| `IsHole` | `bool` | Вырезающий объект |
| `IsMaterial` | `bool` | Флаг материала |
| `CanResideInCatalog` | `bool` | Может находиться в каталоге |
| `ChildrenIDs` | | Список ID дочерних объектов |
| `RealView` | | Реально установленный вид |
| `SimpleView` | | Упрощённый вид |
| `SectionView` | | Вид в разрезе |
| `DimensionView` | | Вид с размерами |
| `ViewInXY` | | Вид в плоскости XY |
| `Protected` | `bool` | Защищённый |
| `Public` | `bool` | Публичный |

**Задокументированные методы McParametricObject:**

| Метод | Описание |
|-------|----------|
| `GetParamLocalName(string name)` | Локализованное имя параметра скрипта. Возвращает `null` если параметр не существует у данного типа объекта. **Без `ref`!** |
| `Change(ExValue ev, bool updateRelated)` | Изменить параметр скрипта. `updateRelated=false` — без каскадного пересчёта |
| `CallMethod(...)` | Вызов скриптовой функции |
| `Initialize(elementID)` | Начальная инициализация через ElementID |
| `SetImplementationAndProcess(name)` | Установить исполнение с пересчётом |
| `ConnectToObjects(...)` | Вызов OnConnection из скрипта |
| `Disconnect()` | Удалить все связи объекта |
| `InvalidateView()` | Установить флаг изменения вида |

**Позиционирование параметрического объекта:**
```csharp
paramObj.Origin = new Point3d(x, y, z);
paramObj.DbObject.Update();
```

---

## 11. McObjectManager (статический класс)

**Namespace:** `Multicad.DatabaseServices`  
**Важно: статический класс, нельзя создавать через `new`!**

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `SelectObject` | `(string prompt) → McObjectId` | Выбор одного объекта |
| `SelectObjects` | `(string prompt) → McObjectId[]` | Выбор нескольких объектов |
| `SelectObjects` | `(ObjectFilter filter) → McObjectId[]` | Выбор с фильтром |
| `Add2Document` | `(McDbObject obj)` | Добавить в текущий документ |
| `Add2Document` | `(McDbObject obj, McDocument doc)` | Добавить в указанный документ |
| `GetObject` | `(McObjectId id) → McObject` | Получить объект по ID |
| `CloneGroup` | `(...)` | Клонировать группу объектов |
| `Erase` | `(McObjectId id)` | Удалить объект |
| `Create` | `(Guid clsid)` | Создать объект по CLSID |

```csharp
// Выбор объекта
string prompt = "\nВыберите объект: ";
McObjectId id = McObjectManager.SelectObject(prompt);

// Добавление в документ
McObjectManager.Add2Document(dbObject);
```

---

## 12. McDocumentsManager

**Namespace:** `Multicad.DatabaseServices`

| Метод | Описание |
|-------|----------|
| `GetActiveDoc()` | Получить активный документ → `McDocument` |

---

## 13. McDocument

**Namespace:** `Multicad.DatabaseServices`  
**Важно: НЕТ свойства `Editor`, НЕТ свойства `Database`!**

| Метод | Описание |
|-------|----------|
| `AddObject(obj)` | Добавить объект |
| `AddObjects(list)` | Добавить несколько объектов |
| `GetActiveSheet()` | Активный лист |
| `GetBlock(name)` | Получить блок |
| `CopyObjects(list)` | Копировать объекты |
| `SaveAs(path)` | Сохранить как |

---

## 14. McContext

**Namespace:** `Multicad.AplicationServices`  
Контекст приложения — настройки, версия, команды.

| Метод | Описание |
|-------|----------|
| `ExecuteCommand(cmd)` | Выполнить команду |
| `GetVersion(type)` | Получить версию |
| `GetDataPath(type)` | Пути приложения |
| `PopupError(msg)` | Показать ошибку |
| `PopupWarning(msg)` | Показать предупреждение |
| `PopupNotification(msg)` | Показать уведомление |
| `SendOption(opt)` | Послать опцию в команду |
| `TerminateCommand()` | Прервать текущую команду |
| `GetProgress()` | Прогресс-бар |

**Нет метода `PutMessage`!** Для вывода в консоль — использовать HostMgd.

---

## 15. Вывод сообщений в консоль nanoCAD

Через HostMgd (рекомендуется):
```csharp
using HostMgd.ApplicationServices;

Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nСообщение");
```

---

## 16. Геометрия — Multicad.Geometry

### Point3d
```csharp
Point3d pt = new Point3d(x, y, z);
Point3d.Origin    // (0, 0, 0)
pt.X, pt.Y, pt.Z  // координаты
pt + vector       // смещение
pt - pt2          // → Vector3d
```

### Vector3d
```csharp
Vector3d v = new Vector3d(x, y, z);
v.X, v.Y, v.Z
v.Length
Vector3d.XAxis   // (1, 0, 0)
Vector3d.YAxis   // (0, 1, 0)
Vector3d.ZAxis   // (0, 0, 1)
v.Normalize()
v.TransformBy(matrix)
v + v2, v - v2, v * scalar
```

### Matrix3d
```csharp
Matrix3d.Identity
Matrix3d.Displacement(Vector3d v)       // матрица смещения
Matrix3d.Rotation(angle, axis, center)  // матрица поворота
Matrix3d.Scaling(scale, center)         // матрица масштабирования
m1 * m2                                 // перемножение матриц
```

**Примеры:**
```csharp
// Перемещение на 10 единиц по X:
Vector3d shift = new Vector3d(10, 0, 0);
Matrix3d moveMatrix = Matrix3d.Displacement(shift);

// Поворот на 90° вокруг оси Z в точке начала координат:
Matrix3d rotMatrix = Matrix3d.Rotation(
    Math.PI / 2,      // угол в радианах (90° = π/2, 180° = π, 45° = π/4)
    Vector3d.ZAxis,   // ось вращения
    Point3d.Origin    // центр вращения
);

// Поворот вокруг собственной точки вставки объекта:
Matrix3d rotAroundSelf = Matrix3d.Rotation(
    Math.PI / 4,      // 45°
    Vector3d.ZAxis,
    paramObj.Origin   // центр = сама точка объекта
);

// Масштабирование в 2 раза:
Matrix3d scaleMatrix = Matrix3d.Scaling(2.0, Point3d.Origin);

// Комбинирование — сначала поворот, потом перемещение:
Matrix3d combined = moveMatrix * rotMatrix;

// Применение к объекту:
paramObj.DbEntity.Transform(combined); // НЕ TransformBy!
paramObj.DbObject.Update();
```

### Polyline3d
```csharp
// Свойства
polyline.Points       // → коллекция точек (Count, [i])
polyline.Vertices     // → VertexDataOfPolyline
polyline.ClosedLogically  // bool — замкнута?
polyline.IsClosed         // bool

// Получение вершин через Points:
var points = polyline.Points;
for (int i = 0; i < points.Count; i++)
    Point3d pt = points[i];

// Получение вершин через Vertices:
var vertList = polyline.Vertices.GetVertices();
foreach (var vert in vertList)
    if (vert.IsPoint()) Point3d pt = vert.Point;
```

---

## 17. DbPolyline (стандартный объект)

**Namespace:** `Multicad.DatabaseServices.StandardObjects`

```csharp
// Проверка что объект является полилинией:
McObject obj = id.GetObject();
if (obj is DbPolyline dbPoly)
{
    Polyline3d pline = dbPoly.Polyline;
    bool closed = pline.ClosedLogically;
}
```

---

## 19a. McProperty

**Namespace:** `Multicad`  
**Наследует:** `PropertyDescriptor` (стандартный .NET) + `IMcProperty`  
Элемент коллекции `DbEntity.ObjectProperties`. Представляет один параметр объекта.

| Член | Описание |
|------|----------|
| `Name` | Системное имя параметра (напр. `"rOtmetkaZemli"`) |
| `PropertyType` | Тип значения (`System.Double`, `System.String` и т.д.) |
| `IsReadOnly` | Только для чтения? |
| `GetValue(component)` | Прочитать значение. **`component` = сам `McParametricObject`** |
| `SetValue(component, value)` | Записать значение. **`component` = сам `McParametricObject`** |
| `GetParamLocalName(name)` | Локализованное имя параметра |
| `Source` | Источник свойства |
| `UnitCategory`, `UnitName` | Единицы измерения |

```csharp
// Чтение параметра скрипта СПДС:
foreach (McProperty prop in paramObj.DbEntity.ObjectProperties)
{
    if (prop.Name == "rOtmetkaZemli")
    {
        double value = (double)prop.GetValue(paramObj); // component = paramObj!
        break;
    }
}

// Запись параметра скрипта СПДС:
foreach (McProperty prop in paramObj.DbEntity.ObjectProperties)
{
    if (prop.Name == "rOtmetkaZemli")
    {
        prop.SetValue(paramObj, 14.5); // component = paramObj!
        break;
    }
}
```

⚠️ **`component` в `GetValue`/`SetValue` = сам `McParametricObject`**, не `DbObject` и не `DbEntity`.  
⚠️ **`DbObject.Params`** содержит настройки приложения (строка БД, версия и т.д.) — **не параметры скрипта**.  
⚠️ **`SetValue` vs `Change()`:** если новое значение совпадает со старым, `SetValue` может **не запустить пересчёт скрипта** — ручки объекта не обновятся. `Change(ExValue, false)` запускает пересчёт **всегда**, независимо от того, изменилось ли значение. Предпочитайте `Change()` когда объект уже перемещён через `Transform`.

---

## 19b. ExValue

**Namespace:** `Multicad`  
**Implements:** `IDisposable`  
Контейнер «имя + значение» для параметрических объектов. Используется в `Change()`.

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Name` | `string` | Системное имя параметра |
| `Value` | `object` | Значение (любой тип) |
| `AsNumber` | `double` | Значение как число |
| `AsString` | `string` | Значение как строка |
| `AsPoint` | `Point3d` | Значение как точка |
| `AsVector` | `Vector3d` | Значение как вектор |
| `AsPlane` | | Значение как плоскость |
| `IsUndefined` | `bool` | Значение не определено |
| `ValueType` | | Тип хранимого значения |

| Метод | Описание |
|-------|----------|
| `Clone()` | Клонировать |
| `SetPlane(...)` | Установить значение-плоскость |
| `TransformBy(matrix)` | Трансформировать |

```csharp
// Создание и передача в Change():
using var ev = new ExValue();
ev.Name  = "rOtmetkaZemli";
ev.Value = 14.263;           // Value — тип object, принимает double
paramObj.Change(ev, false);  // false = без каскадного пересчёта
```

---

## 19c. McDbObject.Parameters

**Namespace:** `Multicad.DatabaseServices`  
**Implements:** `IDisposable`  

⚠️ **Это НЕ параметры скрипта объекта!** Содержит настройки приложения:  
строку подключения к БД, версию, настройки стандартов и т.д.

| Метод | Аргумент | Описание |
|-------|----------|----------|
| `GetParam(int id)` | индекс | Возвращает сырое значение (`String`/`Double`/`Int32`/`null`) |
| `GetStringParam(int id)` | индекс | Строковое значение по индексу |
| `GetDoubleParam(int id)` | индекс | Числовое значение по индексу |
| `GetIntParam(int id)` | индекс | Целочисленное значение по индексу |
| `GetBoolParam(int id)` | индекс | Булево значение по индексу |
| `SetParam(int id, object)` | индекс | Записать значение |
| `GetRedefined()` | — | `List<int>` ID переопределённых параметров |
| `MatchParameters(from)` | Parameters | Скопировать параметры из другого объекта |
| `RemoveRedefinition(int id)` | индекс | Удалить переопределение |

**Правильный способ доступа к параметрам скрипта СПДС — через `DbEntity.ObjectProperties` (McProperty)!**

---

---

## 18. Паттерны работы с объектами

### ⚠️ ВАЖНО: McParametricObject ≠ McCustomBase
Это **разные ветки** иерархии! `is McCustomBase` для параметрического объекта вернёт `false`.

```
McObject : IMcSerializable, IMcStreamSerializable, IDisposable
├── McEntity
│   ├── McCustomBase  (Multicad.CustomObjectBase) ← кастомные объекты
│   └── McCdEntity : IMcOverlay (Multicad.DatabaseServices)
│       └── McParametricObject : I3dQueryable (Multicad.Objects) ← СПДС объекты
└── McPropertySource
    └── McDbObject : ICloneable
        └── McDbEntity
```

### Выбор и проверка типа
```csharp
string prompt = "\nВыберите объект: ";
McObjectId id = McObjectManager.SelectObject(prompt);
if (id.IsNull) return;

McObject obj = id.GetObject();

// Параметрический объект СПДС:
if (obj is McParametricObject paramObj) { ... }

// Полилиния:
if (obj is DbPolyline dbPoly) { ... }

// Любой кастомный объект:
if (obj is McCustomBase customBase) { ... }
```

### Клонирование параметрического объекта
```csharp
McParametricObject CloneParamObj(McParametricObject source)
{
    // 1. Клонируем через DbObject
    McDbObject cloned = source.DbObject.Clone() as McDbObject;
    if (cloned == null) return null;

    // 2. Добавляем в текущий документ
    cloned.AddToCurrentDocument();
    // или: McObjectManager.Add2Document(cloned);

    // 3. Получаем бизнес-объект через Entity
    return cloned.Entity as McParametricObject;
}
```

### Позиционирование объекта

> ⚠️ **ВАЖНО: для `McParametricObject` всегда использовать `Transform`, а не `Origin =`!**  
> `Origin =` обновляет только метаданные точки вставки, но **не перемещает физическую геометрию объекта**.  
> В результате ручки (grips) остаются на исходной позиции (месте шаблона), а сам объект визуально не переезжает корректно.  
> `DbEntity.Transform()` перемещает весь объект целиком — геометрию, ручки и метаданные.

```csharp
// ✅ ПРАВИЛЬНО — для McParametricObject (и для McCustomBase):
Vector3d displacement = targetPoint - paramObj.Origin;
Matrix3d matrix = Matrix3d.Displacement(displacement);
paramObj.DbEntity.Transform(matrix);  // НЕ TransformBy!
paramObj.DbObject.Update();

// ❌ НЕПРАВИЛЬНО — ручки остаются на месте шаблона:
// paramObj.Origin = targetPoint;
// paramObj.DbObject.Update();
```

### Проверка и чтение параметра скрипта СПДС
```csharp
// Проверка наличия параметра у данного типа объекта:
bool HasParam(McParametricObject obj, string paramName)
{
    string local = obj.GetParamLocalName(paramName); // без ref!
    return !string.IsNullOrWhiteSpace(local);
}

// Чтение числового параметра:
double? GetParam(McParametricObject obj, string paramName)
{
    if (string.IsNullOrWhiteSpace(obj.GetParamLocalName(paramName)))
        return null;
    foreach (McProperty prop in obj.DbEntity.ObjectProperties)
        if (prop.Name == paramName)
        {
            object val = prop.GetValue(obj); // component = obj
            if (val is double d) return d;
            if (double.TryParse(val?.ToString(), 
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double p))
                return p;
        }
    return null;
}

// Запись числового параметра:
void SetParam(McParametricObject obj, string paramName, double value)
{
    foreach (McProperty prop in obj.DbEntity.ObjectProperties)
        if (prop.Name == paramName)
        {
            prop.SetValue(obj, value); // component = obj
            return;
        }
}

// Альтернативный способ записи — через Change() + ExValue:
void SetParamViaChange(McParametricObject obj, string paramName, double value)
{
    using var ev = new ExValue();
    ev.Name  = paramName;
    ev.Value = value;
    obj.Change(ev, false);
}
```

---

## 19. Полный рабочий пример — InsertParamObject

```csharp
using System;
using System.Collections.Generic;
using Multicad;
using Multicad.CustomObjectBase;
using Multicad.DatabaseServices;
using Multicad.DatabaseServices.StandardObjects;
using Multicad.Geometry;
using Multicad.Objects;
using Multicad.Runtime;
using HostMgd.ApplicationServices;

namespace MyPlugin
{
    public class InsertParamObjectCommand
    {
        private static void Msg(string text)
            => Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(text);

        [CommandMethod("InsertParamObject", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void InsertParamObject()
        {
            // 1. Выбор параметрического объекта
            string p1 = "\nВыберите параметрический объект: ";
            McObjectId srcId = McObjectManager.SelectObject(p1);
            if (srcId.IsNull) return;

            McObject srcObj = srcId.GetObject();
            if (!(srcObj is McParametricObject srcParam))
            {
                Msg("\nНеобходимо выбрать параметрический объект СПДС");
                return;
            }

            // 2. Выбор полилинии
            string p2 = "\nВыберите полилинию: ";
            McObjectId plineId = McObjectManager.SelectObject(p2);
            if (plineId.IsNull) return;

            McObject plineObj = plineId.GetObject();
            if (!(plineObj is DbPolyline dbPoly))
            {
                Msg("\nНеобходимо выбрать полилинию");
                return;
            }

            Polyline3d pline = dbPoly.Polyline;
            if (pline.ClosedLogically)
            {
                Msg("\nПолилиния должна быть незамкнутой");
                return;
            }

            // 3. Получить вершины
            var pts = new List<Point3d>();
            var points = pline.Points;
            for (int i = 0; i < points.Count; i++)
                pts.Add(points[i]);

            // 4. Вставить копии
            foreach (Point3d vertex in pts)
            {
                McDbObject cloned = srcParam.DbObject.Clone() as McDbObject;
                if (cloned == null) continue;

                cloned.AddToCurrentDocument();

                McParametricObject copy = cloned.Entity as McParametricObject;
                if (copy == null) continue;

                copy.Origin = vertex;
                copy.DbObject.Update();
            }

            Msg($"\nВставлено {pts.Count} объектов");
        }
    }
}
```

---

## 20. Диагностика и отладка

### ⚠️ Критическое требование при отладке СПДС-объектов

**При отладке плагина обязательно запускать именно модуль СПДС**, а не платформу nanoCAD или другие вертикалки. Иначе при выборе СПДС-объекта `GetObject()` вернёт объект другого типа, и `is McParametricObject` всегда будет `false`.

### Узнать реальный тип объекта

```csharp
McObject obj = id.GetObject();
// Показывает полное имя класса выбранного объекта:
string typeName = obj.GetType().FullName;
Application.DocumentManager.MdiActiveDocument
    ?.Editor.WriteMessage($"\nТип объекта: {typeName}");
```

### Команда ListParams — просмотр всех параметров объекта

Незаменима при первом знакомстве с объектом — показывает реальные имена параметров и их текущие значения:

```csharp
[CommandMethod("ListParams", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
public void ListParams()
{
    var editor = Application.DocumentManager.MdiActiveDocument?.Editor;

    McObjectId id = McObjectManager.SelectObject("\nВыберите СПДС-объект: ");
    if (id.IsNull) return;

    McObject obj = id.GetObject();
    editor?.WriteMessage($"\nТип: {obj.GetType().FullName}");

    if (!(obj is McParametricObject paramObj))
    {
        editor?.WriteMessage("\nЭто не McParametricObject");
        return;
    }

    editor?.WriteMessage($"\nClassName: {paramObj.ClassName}");
    editor?.WriteMessage($"\nName: {paramObj.Name}");
    editor?.WriteMessage($"\nOrigin: {paramObj.Origin}");
    editor?.WriteMessage("\n--- Параметры скрипта (ObjectProperties) ---");

    foreach (McProperty prop in paramObj.DbEntity.ObjectProperties)
    {
        object val = prop.GetValue(paramObj);
        editor?.WriteMessage(
            $"\n  [{prop.PropertyType.Name}] {prop.Name} = {val}");
    }
}
```

---

## 21. Частые ошибки и их решения

| Ошибка | Причина | Решение |
|--------|---------|---------|
| `AplicationServices` not found | Опечатка | Правильно: `Multicad.AplicationServices` (одна 'p') |
| `IMcDocument` inaccessible | Внутренний интерфейс | Использовать `McDocument` напрямую |
| `McObjectManager` cannot be instantiated | Статический класс | Не использовать `new`, вызывать напрямую |
| `McDocument` no `Editor` | Нет такого свойства | Использовать `HostMgd.ApplicationServices.Application` |
| `TransformBy` not found | Неверное имя | Правильно: `Transform(Matrix3d)` |
| `GetDynProperties` not found | Метода нет | Использовать `CustomProperties` / `ObjectProperties` |
| Объект не распознаётся как параметрический | Неверный тип | Проверять `is McParametricObject`, не `is McCustomBase` |
| Объекты вставлены, но не позиционированы | Нет установки позиции | Установить `paramObj.Origin = point` |
| Ручки (grips) остаются на месте шаблона после клонирования | `Origin =` не перемещает геометрию, только метаданные | Использовать `DbEntity.Transform(Matrix3d.Displacement(...))` вместо `Origin =` |
| Ручки не обновляются у объекта, у которого значение параметра не изменилось | `McProperty.SetValue()` не запускает пересчёт скрипта если значение совпадает | Использовать `Change(ExValue, false)` — запускает пересчёт всегда |
| CS0021: Cannot apply indexing to `Parameters` | Нет индексатора `p[name]` | Использовать `GetParam(int)` / `SetParam(int, val)` — по индексу |
| CS1503: cannot convert `string` to `int` в `GetParam` | `GetParam` принимает int, не string | Читать через `McProperty.GetValue(paramObj)` из `DbEntity.ObjectProperties` |
| CS1503: cannot convert `string` to `int` в `SetParam` | `SetParam` принимает int, не string | Записывать через `McProperty.SetValue(paramObj, val)` или `Change(ExValue, bool)` |
| CS1615: Argument 1 may not be passed with `ref` | `GetParamLocalName` не принимает ref | Правильно: `paramObj.GetParamLocalName(name)` — без `ref` |
| CS0029: `List<int>` → `ExValue[]` | `GetRedefined()` возвращает `List<int>` (ID), не `ExValue[]` | Это ID индексов — использовать `ObjectProperties` вместо `GetRedefined()` |
| `McProperty` has no `Value` property | Свойство называется иначе | Использовать `prop.GetValue(component)` — метод `PropertyDescriptor` |
| `DbObject.Params` не возвращает параметры скрипта | `Params` = настройки приложения | Параметры скрипта — в `DbEntity.ObjectProperties` (тип `McProperty`) |
| `R0 = 0` при чтении через `GetRedefined()` | `GetRedefined()` — только переопределённые | Дефолтные значения из БД там не появляются; читать через `McProperty.GetValue` |
| `ExValue` не имеет конструктора с параметрами `(string, double)` | Нет такой перегрузки | `new ExValue()`, затем `ev.Name = name; ev.Value = val;` |
| Команда не появляется после `NETLOAD` | Отсутствуют флаги регистрации | Обязательно использовать `CommandFlags.NoCheck | CommandFlags.NoPrefix` |

---

## 21. Примечания

- **`Clone()`** возвращает `object` (C++/CLI биндинг) → кастить через `as McDbObject`
- **`McDbObject.Entity`** возвращает бизнес-объект → для параметрических объектов это `McParametricObject`
- **`AddToCurrentDocument()`** добавляет объект в активный документ без указания документа явно
- **Диагностика типов:** `obj.GetType().FullName` покажет реальный тип выбранного объекта
- **`DbObject.Params`** ≠ параметры скрипта. Содержит настройки приложения (строка БД, версия и т.д.). Структура: `[0]=null`, `[1..2]=String` (имена), `[3]=Double`, `[4..7]=Int32` — не параметры объекта
- **Параметры скрипта СПДС** (Открытые/Защищённые параметры Мастера объектов) хранятся в `DbEntity.ObjectProperties` — коллекция `McProperty`
- **`McProperty`** наследует стандартный .NET `PropertyDescriptor`. Ключевые методы: `GetValue(component)` и `SetValue(component, value)`, где `component` = сам `McParametricObject`
- **`GetParamLocalName(name)`** — метод McParametricObject для проверки существования параметра у данного типа объекта. Возвращает `null` если параметра нет. Принимает `string` **без `ref`**
- **`ExValue`** — контейнер имя+значение (namespace `Multicad`). Используется в `Change(ev, false)`. Конструктор без параметров, затем `ev.Name` и `ev.Value`
- **`GetRedefined()`** возвращает `List<int>` — только ID **переопределённых** параметров. Значения из БД по умолчанию там не появляются → не подходит для чтения шаблонных значений
- **Алгоритм вставки по уровню земли:** `Ycommon = Ymin - R0`, для каждой вершины `Origin=(Xi, Ycommon, 0)`, `rOtmetkaZemli = Yi - Ycommon`
- **`MdiActiveDocument` == null** только при автозагрузке через `[NetModules]` до открытия чертежа. При вызове команды пользователем — всегда не null, `?.` не нужен
- **`CommandFlags.NoCheck | CommandFlags.NoPrefix`** — в MultiCAD.NET это обязательная комбинация для регистрации команды, не просто опциональные флаги поведения
- **`Origin =` НЕ перемещает физическую геометрию** `McParametricObject`. Присвоение `Origin` обновляет только метаданные точки вставки — ручки (grips) остаются на исходной позиции. Для корректного перемещения всегда использовать `DbEntity.Transform(Matrix3d.Displacement(target - paramObj.Origin))` + `DbObject.Update()`
- **`Change(ev, false)` vs `McProperty.SetValue()`:** `SetValue` может не запустить пересчёт скрипта если новое значение совпадает со старым (типичная ситуация для объекта в точке Ymin при вставке по уровню земли, где rValue == R0). `Change()` всегда запускает пересчёт безусловно — использовать его когда нужна гарантированная реакция скрипта на установку параметра

