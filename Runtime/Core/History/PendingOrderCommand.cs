using System;
using System.Collections.Generic;
using TMBS.Core.Events;
using TMBS.Core.Pending;
using UnityEngine;

namespace TMBS.Core.History
{
    public sealed class PendingOrderCommand : IImmediateCommand
    {
        private sealed class OrderRecord
        {
            public BoundsInt Bounds;
            public PendingCell[] TemplateCells;
            public PendingState InitialState;
            public int CurrentOrderId;
            public bool WasCancelledByUndo;
        }

        private readonly IPendingStore _store;
        private readonly IEventBus _events;
        private readonly string _instanceId;
        private readonly bool _emitEvents;
        private readonly bool _replaceExisting;
        private readonly List<OrderRecord> _records;

        public PendingOrderCommand(
            IPendingStore store,
            IEventBus events,
            string instanceId,
            List<(BoundsInt bounds, PendingCell[] cells, PendingState initialState)> orders,
            bool emitEvents,
            bool replaceExisting)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _events = events;
            _instanceId = instanceId;
            _emitEvents = emitEvents;
            _replaceExisting = replaceExisting;
            _records = new List<OrderRecord>(orders != null ? orders.Count : 0);

            if (orders != null)
            {
                for (int i = 0; i < orders.Count; i++)
                {
                    _records.Add(new OrderRecord
                    {
                        Bounds = orders[i].bounds,
                        TemplateCells = orders[i].cells,
                        InitialState = orders[i].initialState,
                        CurrentOrderId = 0,
                        WasCancelledByUndo = false
                    });
                }
            }
        }

        public void Execute()
        {
            for (int r = 0; r < _records.Count; r++)
            {
                var rec = _records[r];
                var cells = new PendingCell[rec.TemplateCells.Length];
                Array.Copy(rec.TemplateCells, cells, cells.Length);

                if (_replaceExisting)
                {
                    for (int c = 0; c < cells.Length; c++)
                    {
                        if (_store.TryGetByCell(cells[c].Cell, out var existing, out _))
                        {
                            var existingBounds = existing.Bounds;
                            existing.SetState(PendingState.Cancelled);
                            _store.Remove(existing.Id);
                            if (_emitEvents)
                            {
                                _events?.Publish(new PendingChangedEvent(_instanceId, existing.Id, PendingState.Cancelled));
                                _events?.Publish(new PendingCompletedEvent(_instanceId, existing.Id, existingBounds));
                            }
                        }
                    }
                }

                var pending = new PendingOrder(_instanceId, rec.Bounds, cells, rec.InitialState);
                int id = _store.Add(pending);
                rec.CurrentOrderId = id;
                rec.WasCancelledByUndo = false;

                if (_emitEvents)
                    _events?.Publish(new PendingCreatedEvent(_instanceId, id, rec.Bounds));
            }
        }

        public void Undo()
        {
            for (int r = 0; r < _records.Count; r++)
            {
                var rec = _records[r];
                if (rec.CurrentOrderId > 0 && _store.TryGet(rec.CurrentOrderId, out var pending))
                {
                    var bounds = pending.Bounds;
                    pending.SetState(PendingState.Cancelled);
                    _store.Remove(rec.CurrentOrderId);
                    _events?.Publish(new PendingChangedEvent(_instanceId, rec.CurrentOrderId, PendingState.Cancelled));
                    _events?.Publish(new PendingCompletedEvent(_instanceId, rec.CurrentOrderId, bounds));
                    rec.WasCancelledByUndo = true;
                    rec.CurrentOrderId = 0;
                }
                else
                {
                    rec.WasCancelledByUndo = false;
                    rec.CurrentOrderId = 0;
                }
            }
        }

        public void Redo()
        {
            for (int r = 0; r < _records.Count; r++)
            {
                var rec = _records[r];
                if (!rec.WasCancelledByUndo)
                    continue;

                var freeCells = new List<PendingCell>();
                for (int c = 0; c < rec.TemplateCells.Length; c++)
                {
                    var template = rec.TemplateCells[c];
                    if (!_store.TryGetByCell(template.Cell, out _, out _))
                    {
                        freeCells.Add(new PendingCell(
                            template.Cell,
                            template.Tile,
                            template.BuildableData,
                            template.WorkRequired,
                            0,
                            template.ResourceRequired,
                            0,
                            false,
                            template.TransformMatrix));
                    }
                }

                if (freeCells.Count == 0)
                {
                    rec.WasCancelledByUndo = false;
                    rec.CurrentOrderId = 0;
                    continue;
                }

                var pending = new PendingOrder(_instanceId, rec.Bounds, freeCells.ToArray(), rec.InitialState);
                int id = _store.Add(pending);
                rec.CurrentOrderId = id;
                rec.WasCancelledByUndo = false;

                if (_emitEvents)
                    _events?.Publish(new PendingCreatedEvent(_instanceId, id, rec.Bounds));
            }
        }
    }
}
