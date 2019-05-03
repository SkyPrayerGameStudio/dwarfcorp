using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using DwarfCorp.GameStates;
using DwarfCorp.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System.Text;
using System;

namespace DwarfCorp
{
    // Todo: Lock down.
    public static class ResourceLibrary
    {
        private static Dictionary<String, Resource> Resources = new Dictionary<String, Resource>();
        public static bool IsInitialized = false;

        public static IEnumerable<Resource> GetResourcesByTag(Resource.ResourceTags tag)
        {
            return Resources.Values.Where(resource => resource.Tags.Contains(tag));
        }

        public static Resource GetLeastValuableWithTag(Resource.ResourceTags tag)
        {
            Resource min = null;
            DwarfBux minValue = decimal.MaxValue;
            foreach (var r in Resources.Values.Where(resource => resource.Tags.Contains(tag)))
            {
                if (r.MoneyValue < minValue)
                {
                    minValue = r.MoneyValue;
                    min = r;
                }
            }
            return min;
        }

        public static Resource GetAverageWithTag(Resource.ResourceTags tag)
        {
            List<Resource> applicable = Resources.Values.Where(resource => resource.Tags.Contains(tag)).ToList();
            applicable.Sort((a, b) =>
            {
                if (a == b)
                {
                    return 0;
                }

                if (a.MoneyValue < b.MoneyValue)
                {
                    return -1;
                }
                if (a.MoneyValue == b.MoneyValue)
                {
                    return 0;
                }

                return 1;
            });
            if (applicable.Count == 0)
            {
                return null;
            }

            return applicable[applicable.Count / 2];
        }

        public static Resource GetResourceByName(string name)
        {
            if (!ResourceLibrary.IsInitialized)
                ResourceLibrary.Initialize();

            return Resources.ContainsKey((String) name) ? Resources[name] : null;
        }

        public static bool Exists(String Name)
        {
            if (!IsInitialized) Initialize();
            return Resources.ContainsKey(Name);
        }

        public static IEnumerable<Resource> Enumerate()
        {
            return Resources.Values;
        }

        private static Rectangle GetRect(int x, int y)
        {
            int tileSheetWidth = 32;
            int tileSheetHeight = 32;
            return new Rectangle(x * tileSheetWidth, y * tileSheetHeight, tileSheetWidth, tileSheetHeight);
        }

        public static void Add(Resource resource)
        {
            if (!ResourceLibrary.IsInitialized)
            {
                ResourceLibrary.Initialize();
            }

            Resources[resource.Name] = resource;

            if (resource.Tags.Contains(Resource.ResourceTags.Money))
            {
                EntityFactory.RegisterEntity(resource.Name + " Resource", (position, data) => new CoinPile(EntityFactory.World.ComponentManager, position)
                {
                    Money = data.Has("Money") ? data.GetData<DwarfBux>("Money") : (DwarfBux)64m
                });
            }
            else
            {
                EntityFactory.RegisterEntity(resource.Name + " Resource", (position, data) => new ResourceEntity(EntityFactory.World.ComponentManager, new ResourceAmount(resource, data.GetData<int>("num", 1)), position));   
            }
        }

        public static void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }
            IsInitialized = true;
            string tileSheet = ContentPaths.Entities.Resources.resources;
            Resources = new Dictionary<String, Resource>();

            var resourceList = FileUtils.LoadJsonListFromMultipleSources<Resource>(ContentPaths.resource_items, null, r => r.Name);

            foreach (var resource in resourceList)
            {
                resource.Generated = false;
                Add(resource);
            }
        }
        
        public static Resource CreateAle(String type)
        {
            var baseResource = Resources[type];
            var aleName = String.IsNullOrEmpty(baseResource.AleName) ? type + " Ale" : baseResource.AleName;
            if (!Resources.ContainsKey(aleName))
            {
                Add(new Resource(Resources[ResourceType.Ale])
                {
                    Name = aleName,
                    ShortName = aleName
                });
            }

            return Resources[aleName];
        }

        public static Resource CreateMeal(String typeA, String typeB)
        {
            Resource componentA = Resources[typeA];
            Resource componentB = Resources[typeB];
            Resource toReturn = new Resource(Resources[ResourceType.Meal])
            {
                FoodContent = componentA.FoodContent + componentB.FoodContent,
                Name =
                    TextGenerator.GenerateRandom(new List<string>() {componentA.Name, componentB.Name}, TextGenerator.GetAtoms(ContentPaths.Text.Templates.food)),
                MoneyValue = 2m *(componentA.MoneyValue + componentB.MoneyValue)
            };
            toReturn.ShortName = toReturn.Name;

            if (!Resources.ContainsKey(toReturn.Name))
                Add(toReturn);

            return toReturn;
        }

        public static Resource EncrustTrinket(String resourcetype, String gemType)
        {
            Resource toReturn = new Resource(Resources[resourcetype]);
            toReturn.Name = gemType + "-encrusted " + toReturn.Name;
            if (Resources.ContainsKey(toReturn.Name))
            {
                return Resources[toReturn.Name];
            }

            toReturn.MoneyValue += Resources[gemType].MoneyValue * 2m;
            toReturn.Tags = new List<Resource.ResourceTags>() {Resource.ResourceTags.Craft, Resource.ResourceTags.Precious};
            toReturn.CompositeLayers = new List<Resource.CompositeLayer>();
            toReturn.CompositeLayers.AddRange(Resources[resourcetype].CompositeLayers);
            if (Resources[resourcetype].TrinketData.EncrustingAsset != null)
            {
                toReturn.CompositeLayers.Add(
                    new Resource.CompositeLayer
                    {
                        Asset = Resources[resourcetype].TrinketData.EncrustingAsset,
                        FrameSize = new Point(32, 32),
                        Frame = new Point(Resources[resourcetype].TrinketData.SpriteColumn, Resources[gemType].TrinketData.SpriteRow)
                    });
            }
            toReturn.GuiLayers = new List<TileReference>();
            toReturn.GuiLayers.AddRange(Resources[resourcetype].GuiLayers);
            toReturn.GuiLayers.Add(new TileReference(Resources[resourcetype].TrinketData.EncrustingAsset, Resources[gemType].TrinketData.SpriteRow * 7 + Resources[resourcetype].TrinketData.SpriteColumn));
            Add(toReturn);
            return toReturn;
        }

        public static Resource GenerateTrinket(String baseMaterial, float quality)
        {
            Resource toReturn = new Resource(Resources[ResourceType.Trinket]);

            string[] names =
            {
                "Ring",
                "Bracer",
                "Pendant",
                "Figure",
                "Earrings",
                "Staff",
                "Crown"
            };

            int[] tiles =
            {
                0,
                1,
                2,
                3,
                4,
                5,
                6
            };

            float[] values =
            {
                1.5f,
                1.8f,
                1.6f,
                3.0f,
                2.0f,
                3.5f,
                4.0f
            };

            int item = MathFunctions.Random.Next(names.Count());

            string name = names[item];
            Point tile = new Point(tiles[item], Resources[baseMaterial].TrinketData.SpriteRow);
            toReturn.MoneyValue = values[item]*Resources[baseMaterial].MoneyValue * 3m * quality;

            string qualityType = "";

            if (quality < 0.5f)
            {
                qualityType = "Very poor";
            }
            else if (quality < 0.75)
            {
                qualityType = "Poor";
            }
            else if (quality < 1.0f)
            {
                qualityType = "Mediocre";
            }
            else if (quality < 1.25f)
            {
                qualityType = "Good";
            }
            else if (quality < 1.75f)
            {
                qualityType = "Excellent";
            }
            else if(quality < 2.0f)
            {
                qualityType = "Masterwork";
            }
            else
            {
                qualityType = "Legendary";
            }

            toReturn.Name = baseMaterial + " " + name + " (" + qualityType + ")";
            if (Resources.ContainsKey(toReturn.Name))
            {
                return Resources[toReturn.Name];
            }
            toReturn.Tint = Resources[baseMaterial].Tint;
            toReturn.CompositeLayers = new List<Resource.CompositeLayer>(new Resource.CompositeLayer[]
            {
                new Resource.CompositeLayer
                {
                    Asset = Resources[baseMaterial].TrinketData.BaseAsset,
                    FrameSize = new Point(32, 32),
                    Frame = tile
                }
            });
            
            Resource.TrinketInfo trinketInfo = Resources[baseMaterial].TrinketData;
            trinketInfo.SpriteColumn = tile.X;
            toReturn.TrinketData = trinketInfo;
            toReturn.GuiLayers = new List<TileReference>() {new TileReference(Resources[baseMaterial].TrinketData.BaseAsset, tile.Y*7 + tile.X)};
            Add(toReturn);
            toReturn.ShortName = baseMaterial + " " + names[item];
            return toReturn;
        }
        
        public static Resource CreateBread(String component)
        {
            Resource toReturn = new Resource(Resources[ResourceType.Bread])
            {
                Name = component + " Bread"
            };
            toReturn.ShortName = toReturn.Name;

            if (!Resources.ContainsKey(toReturn.Name))
                Add(toReturn);

            return toReturn;
        }
    }

}
