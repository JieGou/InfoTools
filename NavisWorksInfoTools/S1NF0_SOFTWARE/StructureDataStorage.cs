﻿using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NavisWorksInfoTools.Constants;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using Win = System.Windows;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using System.IO;
using WinForms = System.Windows.Forms;
using System.Xml.Serialization;
using NavisWorksInfoTools.S1NF0_SOFTWARE.XML.St;
using NavisWorksInfoTools.S1NF0_SOFTWARE.XML.Cl;


//TODO: На будущее: учесть то, что текущий документ может меняться, могут переоткрываться другие документы.
//если в нэвисе открыли другой документ или текущий документ изменился как угодно, то все изменения структуры должны сбрасываться

namespace NavisWorksInfoTools.S1NF0_SOFTWARE
{
    public class StructureDataStorage
    {
        //public const string NEW_CLASS_NAME = "Autogenerated";
        //public const string NEW_CLASS_NAME_IN_PLURAL = "Autogenerated";

        private Document doc = null;
        string stPath = null;
        string clPath = null;
        ComApi.InwOpState3 oState = null;
        public Structure Structure { get; set; } = null;
        public Classifier Classifier { get; set; } = null;
        private List<string> propCategories = new List<string>() { "LcOaPropOverrideCat" };

        /// <summary>
        /// Имена уровней по умолчанию. Должен быть один уровень для папок и один уровень для конечных элементов
        /// Если структура содержит уровни с другими именами, то они и используются
        /// </summary>
        public string[] DefDetailLevels = { "Level 1", "Level 2" };

        /// <summary>
        /// Классы, к которым будут отнесены объекты, не содержащие свойств для соответствующих уровней
        /// </summary>
        public Class[] DefClassesWithNoProps { get; private set; } = { null, null };

        //имена соответствующих классов без свойств: для папок и для конечных элементов
        //Дефолтные классы обязательно должны иметь такие имена!
        //public readonly string[] DEFAULT_CLASS_NAME = { "DefaultFolder", "DefaultGeometry" };
        //public readonly string[] DEFAULT_CLASS_NAME_IN_PLURAL = { "DefaultFolder", "DefaultGeometry" };



        /// <summary>
        /// Ключ - название всех свойств, отсортированных по алфавиту и срощенных в одну строку
        /// </summary>
        public Dictionary<string, Class> ClassLookUpByProps { get; private set; } = new Dictionary<string, Class>();

        /// <summary>
        /// Ключ - код класса
        /// </summary>
        public Dictionary<string, Class> ClassLookUpByCode { get; private set; } = new Dictionary<string, Class>();


        //TODO: Учесть, что id может быть не строкой
        // Поиск ID с помощью Search API Navis
        private static NamedConstant tabCN = new NamedConstant("LcOaPropOverrideCat", S1NF0_DATA_TAB_DISPLAY_NAME);
        private static NamedConstant idCN = new NamedConstant(ID_PROP_DISPLAY_NAME, ID_PROP_DISPLAY_NAME);
        //private Search searchForCertainID = null;
        //private SearchCondition searchForCertainIDCondition = null;

        //Поиск ID по словарю
        // Словарь всех элементов геометрии модели по их id
        public Dictionary<string, ModelItem> AllItemsLookup { get; private set; } = null;


        //Коллекция всх объектов геометрии документа Navis
        //public ModelItemCollection AllNotHiddenGeometryModelItems { get; private set; } = null;

        /// <summary>
        /// Ключ - ID
        /// </summary>
        public Dictionary<string, XML.St.Object> AddedGeometryItemsLookUp { get; private set; } = new Dictionary<string, XML.St.Object>();


        public StructureWindow StructureWindow { get; set; } = null;
        public StructureDataStorage(Document doc, string stPath, string clPath, Structure structure,
            Classifier classifier, bool newStructureCreationBySelSets = false, List<string> propCategories = null)
        {
            this.doc = doc;
            this.stPath = stPath;
            this.clPath = clPath;
            this.oState = ComApiBridge.ComApiBridge.State;
            this.Structure = structure;
            this.Classifier = classifier;

            if (propCategories != null)
            {
                this.propCategories = propCategories;
            }

            //Все новые классы будут создаваться на 2 DetailLevel. 1 - папки, 2 - конечные элементы 
            //(это относится только к вновь создаваемым объектам)
            //Значит должно быть 2 класса без свойств. 1 - для папок, второй для конечных элементов!

            //если уровней меньше, чем должно быть, то нужно добавить недостающие
            for (int i = classifier.DetailLevels.Count; i < DefDetailLevels.Length; i++)
            {
                classifier.DetailLevels.Add(DefDetailLevels[i]);
            }

            DefDetailLevels[0] = classifier.DetailLevels[0];
            DefDetailLevels[1] = classifier.DetailLevels[1];
            foreach (string lvlName in DefDetailLevels)
            {
                if (String.IsNullOrWhiteSpace(lvlName))
                {
                    throw new Exception("DetailLevel должен быть не пустой строкой");
                }
            }

            //Построить словари для быстрого поиска классов
            foreach (Class c in classifier.NestedClasses)
            {
                SurveyClass(c);
            }

            //Всегда должны быть заданы все необходимые DefaultClass (сейчас их 2 - для папок и для конечных элементов)
            for (int i = 0; i < DefClassesWithNoProps.Length; i++)
            {
                if (DefClassesWithNoProps[i] == null)
                {
                    DefClassesWithNoProps[i] = CreateNewClass(Classifier.DefaultClasses[i] /*DEFAULT_CLASS_NAME[i]*/,
                        Classifier.DefaultClasses[i]/*DEFAULT_CLASS_NAME_IN_PLURAL[i]*/, i);
                }
            }

            #region Поиск всех объектов геометрии с id с помощью Search API. Нельзя настроить на поиск только не скрытых элементов
            /*
                //Все объекты модели геометрии с id
                Search searchForAllIDs = new Search();
                searchForAllIDs.Selection.SelectAll();
                //searchForIDs.PruneBelowMatch = false;

                ConfigureSearchForAllNotHiddenGeometryItemsWithIds(searchForAllIDs);

                //ModelItemCollection allModelItemCollection;
                AllNotHiddenGeometryModelItems = searchForAllIDs.FindAll(doc, false);

                int n = AllNotHiddenGeometryModelItems.Count;


                //Сформировать объект Search для поиска конкретного значения Id. МНОГОКРАТНЫЙ ПОИСК РАБОТАЕТ МЕДЛЕННО
                //searchForCertainID = new Search();
                //searchForCertainID.Selection.CopyFrom(allModelItemCollection);
                //searchForCertainIDCondition = new SearchCondition(tabCN, idCN, SearchConditionOptions.None, SearchConditionComparison.Equal, new VariantData());

                //ПОЧЕМУ-ТО ОБХОД ОБЪЕКТОВ ПРОИСХОДИТ ОЧЕНЬ МЕДЛЕННО НА БОЛЬЩИХ МОДЕЛЯХ
                //Поэтому у пользователя есть возможность скрыть часть модели, чтобы не загружать ее всю в словарь за один раз
                allItemsLookup = new Dictionary<string, ModelItem>();
                foreach (ModelItem item in AllNotHiddenGeometryModelItems)
                {
                    DataProperty idProp = item.PropertyCategories
                                    .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                                    ID_PROP_DISPLAY_NAME);
                    string key = Utils.GetDisplayValue(idProp.Value);
                    allItemsLookup[key] = item;
                }
                */
            #endregion

            //Обход всей модели через рекурсию НЕ БЫСТРЕЕ. Но можно искать только не скрытые элементы
            if (!newStructureCreationBySelSets)//если структура создается с нуля, то этот шаг не нужен!
            {
                AllItemsLookup = new Dictionary<string, ModelItem>();
                RecurseSearchForAllNotHiddenGeometryItemsWithIds(doc.Models.RootItems, AllItemsLookup);

                //Обход всех объектов
                //Просмотреть все объекты, не имеющие вложенных. Какие из них уже присутствуют в документе?
                //Построить словарь для быстрого поиска объектов, уже добавленных в дерево
                List<XML.St.Object> nestedObjectsValid; List<XML.St.Object> geometryObjectsCurrDoc;
                List<XML.St.Object> geometryObjectsOtherDoc; List<XML.St.Object> displayObjects;
                List<XML.St.Object> resetObjects;
                SurveyNestedObjects(structure.NestedObjects,
                    out nestedObjectsValid, out geometryObjectsCurrDoc,
                    out geometryObjectsOtherDoc, out displayObjects, out resetObjects);
                structure.NestedObjects = nestedObjectsValid;

                //Если какие-то объекты уже присутствуют в дереве, то уточнить набор свойств и класс для них согласно модели Navis
                foreach (XML.St.Object o in AddedGeometryItemsLookUp.Values)
                {
                    PropertyCategoryCollection categories = o.NavisItem.PropertyCategories;

                    SetObjectProps(o, categories, 1, 1);

                }

                //Создать окно и заполнить TreeView.
                StructureWindow = new StructureWindow(this);
            }

        }

        /// <summary>
        /// НЕЛЬЗЯ НАСТРОИТЬ ПОИСК ТОЛЬКО НЕСКРЫТЫХ ОБЪЕКТОВ
        /// </summary>
        /// <param name="searchForAllIDs"></param>
        public static void ConfigureSearchForAllGeometryItemsWithIds(Search searchForAllIDs, bool idNecessary = true)
        {
            if (idNecessary)
            {
                //Имеет свойство Id
                SearchCondition hasIdCondition = SearchCondition.HasPropertyByCombinedName(tabCN, idCN);
                searchForAllIDs.SearchConditions.Add(hasIdCondition);
            }

            //Является геометрией (проверяется по типу иконки узла Navis)
            NamedConstant namedConstant = new NamedConstant(8, "LcOaNodeIcon"/*, "Геометрия"*/);
            VariantData geometryIcon = new VariantData(namedConstant);
            searchForAllIDs.SearchConditions.Add(
                new SearchCondition(new NamedConstant("LcOaNode", "Элемент"), new NamedConstant("LcOaNodeIcon", "Значок"),
                SearchConditionOptions.IgnoreDisplayNames, SearchConditionComparison.Equal, geometryIcon));

            //Не скрыт. ЭТО НЕ РАБОТАЕТ!!!!
            //VariantData boolFalse = new VariantData(false);
            //searchForAllIDs.SearchConditions.Add(
            //    new SearchCondition(new NamedConstant("LcOaNode", "Элемент"), new NamedConstant("LcOaNodeHidden", "Скрытый"),
            //    SearchConditionOptions.IgnoreDisplayNames, SearchConditionComparison.Equal, boolFalse));


        }


        private static void RecurseSearchForAllNotHiddenGeometryItemsWithIds
            (IEnumerable<ModelItem> items, Dictionary<string, ModelItem> itemsLookup)
        {
            foreach (ModelItem item in items)
            {
                if (!item.IsHidden)
                {
                    if (item.HasGeometry)
                    {
                        DataProperty idProp = item.PropertyCategories
                                    .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                                    ID_PROP_DISPLAY_NAME);
                        if (idProp != null)
                        {
                            string key = Utils.GetDisplayValue(idProp.Value);
                            itemsLookup[key] = item;
                        }
                    }

                    RecurseSearchForAllNotHiddenGeometryItemsWithIds(item.Children, itemsLookup);
                }

            }
        }


        /// <summary>
        /// УДАЛЕНИЕ ДУБЛИКАТОВ СВОЙСТВ
        /// Обход классов. Заполнение словарей для быстрого поиска классов
        /// </summary>
        /// <param name="class"></param>
        private void SurveyClass(Class @class)
        {
            @class.PropsCorrection();
            if (!String.IsNullOrWhiteSpace(@class.Code))//Классы без кода останутся но не попадают в словари
            {
                ClassLookUpByCode.Add(@class.Code, @class);

                if (@class.Properties.Count > 0)
                {
                    string key = GetClassKey(@class.Properties);

                    //Если есть 2 класса с одинаковым набором свойств, то словаре будет только один из них
                    if (!ClassLookUpByProps.ContainsKey(key))
                        ClassLookUpByProps.Add(key, @class);
                }
                else
                {
                    //обнаружен класс без свойств. Он подходит по имени?
                    for (int i = 0; i < Classifier.DefaultClasses.Length; i++)
                    {
                        if (DefClassesWithNoProps[i] == null && @class.Name.Equals(Classifier.DefaultClasses[i]))
                        {
                            DefClassesWithNoProps[i] = @class;
                        }
                    }
                }

            }

            foreach (Class nestedClass in @class.NestedClasses)
            {
                SurveyClass(nestedClass);
            }
        }


        private enum SurveyObjectResult
        {
            RegularObject,
            GeometryObjectCurrDoc,
            GeometryObjectOtherDoc,
            InvalidObject
        }

        /// <summary>
        /// Обход объектов. Объекты геометрии текущего документа попадают в словарь
        /// УДАЛЕНИЕ ДУБЛИКАТОВ СВОЙСТВ если они есть в существующем XML
        /// </summary>
        /// <param name="object"></param>
        /// <returns></returns>
        private SurveyObjectResult SurveyObject(XML.St.Object @object)
        {
            @object.PropsCorrection();
            SurveyObjectResult result = SurveyObjectResult.RegularObject;
            if (String.IsNullOrEmpty(@object.Name) || String.IsNullOrEmpty(@object.ClassCode))
            {
                result = SurveyObjectResult.InvalidObject;
            }
            else if (@object.NestedObjects.Count == 0 && !String.IsNullOrEmpty(@object.SceneObjectId))
            {
                string[] splitted = @object.SceneObjectId.Split('|');
                if (splitted.Length > 1)
                {
                    string id = splitted[1];

                    if (AddedGeometryItemsLookUp.ContainsKey(id))
                    {
                        //Если есть объекты с дублирующимися id, то их нужно будет удалить
                        result = SurveyObjectResult.InvalidObject;
                    }
                    else
                    {
                        ModelItem item = SearchForIDInCurrDoc(id);
                        if (item != null && item.HasGeometry)
                        {
                            //Это объект геометрии этого документа
                            @object.NavisItem = item;
                            AddedGeometryItemsLookUp.Add(id, @object);
                            result = SurveyObjectResult.GeometryObjectCurrDoc;
                        }
                        else
                        {
                            //Видимо это объект геометрии добавленный в другом документе
                            result = SurveyObjectResult.GeometryObjectOtherDoc;
                        }
                    }
                }
            }

            if (result == SurveyObjectResult.RegularObject)
            {
                List<XML.St.Object> nestedObjectsValid;
                List<XML.St.Object> geometryObjectsCurrDoc;
                List<XML.St.Object> geometryObjectsOtherDoc;
                List<XML.St.Object> displayObjects;
                List<XML.St.Object> resetObjects;
                SurveyNestedObjects(@object.NestedObjects,
                    out nestedObjectsValid, out geometryObjectsCurrDoc,
                    out geometryObjectsOtherDoc, out displayObjects, out resetObjects);

                @object.NestedObjects = nestedObjectsValid;
                @object.NestedDisplayObjects = displayObjects;//больше не поменяются
                @object.NestedGeometryObjectsCurrDoc = geometryObjectsCurrDoc;
                @object.NestedGeometryObjectsOtherDoc = geometryObjectsOtherDoc;//больше не поменяются
                @object.ResetNestedObjects = resetObjects;//может поменяться если удалять все объекты, включая те, что из других документов
            }

            return result;
        }



        private void SurveyNestedObjects(List<XML.St.Object> nestedObjects,
            out List<XML.St.Object> nestedObjectsValid,
            out List<XML.St.Object> geometryObjectsCurrDoc,
            out List<XML.St.Object> geometryObjectsOtherDoc,
            out List<XML.St.Object> displayObjects,
            out List<XML.St.Object> resetObjects)
        {
            nestedObjectsValid = new List<XML.St.Object>();
            geometryObjectsCurrDoc = new List<XML.St.Object>();
            geometryObjectsOtherDoc = new List<XML.St.Object>();
            displayObjects = new List<XML.St.Object>();
            resetObjects = new List<XML.St.Object>();
            foreach (XML.St.Object nestedObject in nestedObjects)
            {
                switch (SurveyObject(nestedObject))
                {
                    case SurveyObjectResult.RegularObject:
                        nestedObjectsValid.Add(nestedObject);
                        displayObjects.Add(nestedObject);
                        resetObjects.Add(nestedObject);
                        break;
                    case SurveyObjectResult.GeometryObjectCurrDoc:
                        nestedObjectsValid.Add(nestedObject);
                        geometryObjectsCurrDoc.Add(nestedObject);
                        break;
                    case SurveyObjectResult.GeometryObjectOtherDoc:
                        nestedObjectsValid.Add(nestedObject);
                        geometryObjectsOtherDoc.Add(nestedObject);
                        resetObjects.Add(nestedObject);
                        break;
                    case SurveyObjectResult.InvalidObject:
                        break;
                }

            }
        }



        private ModelItem SearchForIDInCurrDoc(string guid)
        {
            //Search API - САМОЕ МЕДЛЕННОЕ
            //VariantData oData = VariantData.FromDisplayString(guid);
            //SearchCondition searchForIDCondition = searchForCertainIDCondition.EqualValue(oData);
            //searchForCertainID.SearchConditions.CopyFrom(new SearchConditionCollection() { searchForIDCondition });

            //return searchForCertainID.FindFirst(doc, false);

            ModelItem item = null;
            AllItemsLookup.TryGetValue(guid, out item);
            return item;

            //ЭТО ПОБЫСТРЕЕ ЧЕМ ПЕРВЫЙ ВАРИАНТ
            //VariantData oData = VariantData.FromDisplayString(guid);
            //IEnumerable<ModelItem> modelItems = allModelItemCollection.Where(new SearchCondition(tabCN, idCN, SearchConditionOptions.None, SearchConditionComparison.Equal, oData));
            //if (modelItems!=null && modelItems.Count()>0)
            //{
            //    return modelItems.First();
            //}
            //else
            //{
            //    return null;
            //}
        }

        /// <summary>
        /// Заполнение свойств объекта в соответствии с классами. Присвоение ссылки на класс. Создание класса если еще нет
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="categories"></param>
        private void SetObjectProps(XML.St.Object obj, PropertyCategoryCollection categories, int defClassNum, int defClassLevelNum)
        {
            List<XML.Cl.Property> clProps = null;
            List<XML.St.Property> stProps = null;
            string actualKey = AnalizeNavisProps(categories, out clProps, out stProps);

            //Сначала присвоить правильный класс
            Class @class = null;
            //Проверить есть ли у объекта уже ссылка на класс
            if (!String.IsNullOrEmpty(obj.ClassCode))
            {
                //Найти этот класс
                ClassLookUpByCode.TryGetValue(obj.ClassCode, out @class);
                if (@class != null)
                {
                    //Если класс есть, то проверить его свойства
                    string currKey = GetClassKey(@class.Properties);
                    if (currKey == null || !currKey.Equals(actualKey))
                    {
                        //Если этот класс не подходит по свойствам, то он должен быть переназначен
                        @class = null;
                    }
                }
            }

            if (@class == null)
            {
                //Проверить есть ли класс подходящий по свойствам
                if (actualKey == null)
                {
                    //Свойств нет - присвоить дефолный класс без свойств
                    @class = DefClassesWithNoProps[defClassNum];
                }
                else
                {
                    //Поиск существующего класса подходящего по свойствам
                    ClassLookUpByProps.TryGetValue(actualKey, out @class);
                    if (@class == null)
                    {
                        //Подходящий класс не найден. Создать его
                        @class = CreateNewClass(Classifier.ClassName/*NEW_CLASS_NAME*/,
                            Classifier.ClassName/*NEW_CLASS_NAME_IN_PLURAL*/,
                            defClassLevelNum, actualKey, clProps);
                    }
                }
            }

            //Присвоить объекту ссылку на актуальный класс
            obj.ClassCode = @class.Code;

            //Задать свойства для объекта
            obj.Properties = stProps;
        }

        /// <summary>
        /// Создает класс и добавляет его во все нужные словари
        /// </summary>
        /// <param name="Name"></param>
        /// <param name=""></param>
        /// <returns></returns>
        private Class CreateNewClass(string name, string nameInPlural, int defLevelNum, string propKey = null, List<XML.Cl.Property> clProps = null)
        {
            //string code = GetUniqueClassCode();
            string code = Guid.NewGuid().ToString();
            Class @class = new Class()
            { Name = name, NameInPlural = nameInPlural, DetailLevel = DefDetailLevels[defLevelNum], Code = code };
            ClassLookUpByCode.Add(code, @class);
            Classifier.NestedClasses.Add(@class);

            if (propKey != null && clProps != null)
            {
                @class.Properties = clProps;
                ClassLookUpByProps.Add(propKey, @class);
            }

            return @class;
        }


        private static string GetClassKey(List<XML.Cl.Property> clProps)
        {
            string key = null;

            if (clProps.Count > 0)
            {
                //может сортироваться прямо в самом классе
                clProps.Sort();
                key = String.Join("", clProps);
            }

            return key;
        }

        /// <summary>
        /// Возвращает ключ для словаря посика класса по набору свойств Navis
        /// </summary>
        /// <param name="categories"></param>
        /// <param name="clProps"></param>
        /// <param name="stProps"></param>
        /// <returns></returns>
        private string AnalizeNavisProps(PropertyCategoryCollection categories,
            out List<XML.Cl.Property> clProps, out List<XML.St.Property> stProps)
        {
            clProps = new List<XML.Cl.Property>();
            stProps = new List<XML.St.Property>();

            foreach (PropertyCategory c in categories)
            {
                if (
                        //(c.Name.Equals("LcOaPropOverrideCat")
                        //&& c.DisplayName != S1NF0_DATA_TAB_DISPLAY_NAME)
                        //|| c.Name.Equals("LcRevitData_Parameter")//так же брать вкладки свойств из Revit и Civil
                        //|| c.Name.Equals("LcRevitData_Type")
                        //|| c.Name.Equals("LcRevitData_Element")
                        //|| c.Name.Equals("LcRevitMaterialProperties")
                        //|| c.Name.Equals("AecDbPropertySet")

                        propCategories.Contains(c.Name)
                        && !(c.Name.Equals("LcOaPropOverrideCat") && c.DisplayName == S1NF0_DATA_TAB_DISPLAY_NAME)
                    )
                {
                    foreach (DataProperty p in c.Properties)
                    {
                        //Удалять все символы, которые не подходят для XML
                        string name = Common.Utils.RemoveNonValidXMLCharacters(p.DisplayName);
                        string tag = Common.Utils.RemoveNonValidXMLCharacters(c.DisplayName);
                        string value = Common.Utils.RemoveNonValidXMLCharacters(Utils.GetDisplayValue(p.Value));

                        clProps.Add(new XML.Cl.Property() { Name = name, Tag = tag });
                        stProps.Add(new XML.St.Property() { Name = name, Value = value });
                    }
                }
            }
            //Исправление свойств в соответстствии с требованиями Мякиша
            clProps = XML.Cl.Class.PropsCorrection(clProps);
            stProps = XML.St.Object.PropsCorrection(stProps);

            clProps.Sort();
            return GetClassKey(clProps);
        }


        private string GetUniqueClassCode()
        {
            int n = 1;
            while (ClassLookUpByCode.ContainsKey(n.ToString()))
            {
                n++;
            }
            return n.ToString();
        }


        /// <summary>
        /// ВЫЗЫВАЕТСЯ НАЖАТИЕМ КНОПКИ В ОКНЕ
        /// Создает новый объект в дереве структуры и добавляет в словарь
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="item"></param>
        public void CreateNewModelObject(XML.St.Object parent, ModelItem item)
        {
            string replacementName, baseName, exportName, strId;
            bool contains = ItemAdded(item, out baseName, out exportName, out replacementName, out strId);
            if (strId == null)
            {
                //если id нет, то принять за id обычное имя
                strId = baseName;
                replacementName = baseName;

                contains = AddedGeometryItemsLookUp.ContainsKey(strId);
                if (contains)
                {

                }
            }

            if (!contains /*&& strId != null*/)//Добавлять только если объект еще не был добавлен и имеет id
            {



                XML.St.Object @object = new XML.St.Object()
                {
                    Name = !String.IsNullOrWhiteSpace(exportName) ? exportName : baseName,//replacementName - имя вместе с guid //baseName - имя в модели Navis. Оно не подходит так как может быть определено служебное свойство для имени при экспорте
                    SceneObjectId = replacementName,
                    NavisItem = item,
                };

                //Настрока ссылки на класс и заполнение списка свойств в соответствии со свойствами Navis
                SetObjectProps(@object, item.PropertyCategories, 1, 1);

                //Добавление в соответствующие списки родителя
                if (parent != null)
                {
                    parent.NestedObjects.Add(@object);
                    parent.NestedGeometryObjectsCurrDoc.Add(@object);
                }
                else
                {
                    Structure.NestedObjects.Add(@object);
                }



                //Добавление в словарь поиска по id
                AddedGeometryItemsLookUp.Add(strId, @object);
            }

        }

        /// <summary>
        /// Создание нового объекта-контейнера
        /// Без свойств
        /// ВЫЗЫВАЕТСЯ ПРИ СОЗДАНИИ НОВОЙ СТРУКТУРЫ ИЗ СОХРАНЕННЫХ НАБОРОВ ВЫБОРА
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="name"></param>
        public XML.St.Object CreateNewContainerObject(XML.St.Object parent, string name)
        {
            //Создание объекта
            XML.St.Object @object = new XML.St.Object()
            {
                Name = name,
                ClassCode = DefClassesWithNoProps[0].Code,
                NestedDisplayObjects = new List<XML.St.Object>(),
                NestedGeometryObjectsCurrDoc = new List<XML.St.Object>(),
                NestedGeometryObjectsOtherDoc = new List<XML.St.Object>(),
                ResetNestedObjects = new List<XML.St.Object>()
            };

            if (parent != null)
            {
                //Добавление в соответствующие списки родителя
                parent.NestedObjects.Add(@object);
                parent.NestedDisplayObjects.Add(@object);
                parent.ResetNestedObjects.Add(@object);
            }
            else
            {
                //Объект добавляется в корень структуры
                Structure.NestedObjects.Add(@object);
            }

            return @object;
        }

        /// <summary>
        /// Проверка, добавлен ли объект в структуру
        /// </summary>
        /// <param name="item"></param>
        /// <param name="baseName"></param>
        /// <param name="replacementName"></param>
        /// <param name="strId"></param>
        /// <returns></returns>
        public bool ItemAdded(ModelItem item, out string baseName, out string exportName, out string replacementName, out string strId)
        {
            baseName = null;
            exportName = null;
            strId = null;
            bool baseNameTrustable;
            object id;
            FBXExport.CreateReplacementName(oState, item, out baseName, out exportName,
                out baseNameTrustable, out replacementName, out id, false);
            if (id != null)
            {
                strId = id.ToString();
                return AddedGeometryItemsLookUp.ContainsKey(strId);
            }
            else
            {
                return false;
            }

        }


        /// <summary>
        /// ВЫЗЫВАЕТСЯ НАЖАТИЕМ КНОПКИ В ОКНЕ
        /// Очистить узел от добавленных к нему объектов геометрии Navis
        /// </summary>
        /// <param name="parent"></param>
        public void ResetNestedObjects(XML.St.Object parent)
        {
            foreach (XML.St.Object obj in parent.NestedGeometryObjectsCurrDoc)
            {
                //Каждый объект геометрии удалить из словаря
                string[] splitted = obj.SceneObjectId.Split('|');
                string id = splitted[1];
                AddedGeometryItemsLookUp.Remove(id);
            }

            //Сброс списка вложенных
            parent.NestedObjects = new List<XML.St.Object>(parent.ResetNestedObjects);
            //Очистить список объектов геометрии
            parent.NestedGeometryObjectsCurrDoc = new List<XML.St.Object>();

            parent.NotifyPropertyChanged();
        }

        public void RemoveNestedObjectsOtherDocuments(XML.St.Object parent)
        {
            //Очистка списка вложенных из других документов
            parent.NestedGeometryObjectsOtherDoc = new List<XML.St.Object>();
            //Список сброса теперь не содержит объекты из других документов
            parent.ResetNestedObjects = new List<XML.St.Object>(parent.NestedDisplayObjects);
            //Общий список всех вложенных объектов теперь не содержит объекты из других документов
            parent.NestedObjects = parent.ResetNestedObjects.Concat(parent.NestedGeometryObjectsCurrDoc).ToList();

            parent.NotifyPropertyChanged();
        }


        /// <summary>
        /// ВЫЗЫВАЕТСЯ НАЖАТИЕМ КНОПКИ В ОКНЕ
        /// Выбрать в документе все объекты геометрии, которые уже добавлены в структуру
        /// </summary>
        public void SelectAdded()
        {
            //ModelItemCollection toSelect = new ModelItemCollection();
            //foreach (KeyValuePair<string, ModelItem> kvp in allItemsLookup)
            //{
            //    if (!AddedGeometryItemsLookUp.ContainsKey(kvp.Key))
            //    {
            //        toSelect.Add(kvp.Value);
            //    }
            //}
            //doc.CurrentSelection.CopyFrom(toSelect);

            ModelItemCollection toSelect = new ModelItemCollection();
            foreach (XML.St.Object obj in AddedGeometryItemsLookUp.Values)
            {
                toSelect.Add(obj.NavisItem);
            }

            doc.CurrentSelection.CopyFrom(toSelect);
        }

        /// <summary>
        /// ВЫЗЫВАЕТСЯ НАЖАТИЕМ КНОПКИ В ОКНЕ
        /// Сериализация структуры и классификатора
        /// </summary>
        public void SerializeStruture()
        {
            //TODO: Перед сериализацией убедиться, что  во всех объектах и во всех классах нет дублирующихся свойств
            //string stDir = Path.GetDirectoryName(stPath);
            //string stName = Path.GetFileNameWithoutExtension(stPath);
            //if (stPath.EndsWith(".st.xml"))
            //{
            //    stName = Path.GetFileNameWithoutExtension(stName);
            //}
            //string stSavePath = Common.Utils.GetNonExistentFileName(stDir, stName, "st.xml");

            //string clDir = Path.GetDirectoryName(clPath);
            //string clName = Path.GetFileNameWithoutExtension(clPath);
            //if (clPath.EndsWith(".cl.xml"))
            //{
            //    clName = Path.GetFileNameWithoutExtension(clName);
            //}
            //string clSavePath = Common.Utils.GetNonExistentFileName(clDir, clName, "cl.xml");

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(Structure));
            using (StreamWriter sw = new StreamWriter(stPath /*stSavePath*/))
            {
                xmlSerializer.Serialize(sw, Structure);
            }

            xmlSerializer = new XmlSerializer(typeof(Classifier));
            using (StreamWriter sw = new StreamWriter(clPath /*clSavePath*/))
            {
                xmlSerializer.Serialize(sw, Classifier);
            }
        }





    }
}
