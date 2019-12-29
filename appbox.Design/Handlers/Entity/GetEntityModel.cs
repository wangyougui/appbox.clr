﻿using System;
using System.Threading.Tasks;
using appbox.Data;
using appbox.Models;
using OmniSharp.Mef;

namespace appbox.Design
{
    /// <summary>
    /// 前端设计器打开实体模型时获取实体模型信息
    /// </summary>
    sealed class GetEntityModel : IRequestHandler
    {
        public async Task<object> Handle(DesignHub hub, InvokeArgs args)
        {
            var modelId = args.GetString();
            var modelNode = hub.DesignTree.FindModelNode(ModelType.Entity, ulong.Parse(modelId));
            if (modelNode == null)
                throw new Exception($"Cannot find EntityModel: {modelId}");

            var model = (EntityModel)modelNode.Model;
            if (model.SysStoreOptions != null)
            {
#if FUTURE
                //注意刷新构建中的索引状态
                await Store.ModelStore.LoadIndexBuildingStatesAsync(modelNode.AppNode.Model, (EntityModel)modelNode.Model);
#endif
            }
            else if (model.SqlStoreOptions != null)
            {
                //set StoreName for SqlStoreOptions
                var storeNode = (DataStoreNode)hub.DesignTree.FindNode(
                    DesignNodeType.DataStoreNode, model.SqlStoreOptions.StoreModelId.ToString());
                if (storeNode == null)
                    throw new Exception($"Cannot find Store: {model.SqlStoreOptions.StoreModelId}");
                model.SqlStoreOptions.StoreModel = storeNode.Model; //set cache
            }

            return modelNode.Model;
        }
    }
}
