package com.example.yakultscanner.data.db;

import androidx.annotation.NonNull;
import androidx.room.EntityDeleteOrUpdateAdapter;
import androidx.room.EntityInsertAdapter;
import androidx.room.RoomDatabase;
import androidx.room.coroutines.FlowUtil;
import androidx.room.util.DBUtil;
import androidx.room.util.SQLiteStatementUtil;
import androidx.sqlite.SQLiteStatement;
import java.lang.Class;
import java.lang.Integer;
import java.lang.Long;
import java.lang.NullPointerException;
import java.lang.Object;
import java.lang.Override;
import java.lang.String;
import java.lang.SuppressWarnings;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import kotlin.Unit;
import kotlin.coroutines.Continuation;
import kotlinx.coroutines.flow.Flow;

@SuppressWarnings({"unchecked", "deprecation", "removal"})
public final class PendingUpdateDao_Impl implements PendingUpdateDao {
  private final RoomDatabase __db;

  private final EntityInsertAdapter<PendingUpdateEntity> __insertAdapterOfPendingUpdateEntity;

  private final EntityDeleteOrUpdateAdapter<PendingUpdateEntity> __deleteAdapterOfPendingUpdateEntity;

  private final EntityDeleteOrUpdateAdapter<PendingUpdateEntity> __updateAdapterOfPendingUpdateEntity;

  public PendingUpdateDao_Impl(@NonNull final RoomDatabase __db) {
    this.__db = __db;
    this.__insertAdapterOfPendingUpdateEntity = new EntityInsertAdapter<PendingUpdateEntity>() {
      @Override
      @NonNull
      protected String createQuery() {
        return "INSERT OR ABORT INTO `pending_updates` (`id`,`set_code`,`set_id`,`item_id`,`item_type`,`serial_number`,`model_number`,`previous_status`,`new_status`,`remark`,`updated_by_user_id`,`updated_by_name`,`created_at`,`retry_count`,`sync_status`,`last_error`,`synced_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)";
      }

      @Override
      protected void bind(@NonNull final SQLiteStatement statement,
          @NonNull final PendingUpdateEntity entity) {
        if (entity.getId() == null) {
          statement.bindNull(1);
        } else {
          statement.bindText(1, entity.getId());
        }
        if (entity.getSetCode() == null) {
          statement.bindNull(2);
        } else {
          statement.bindText(2, entity.getSetCode());
        }
        if (entity.getSetId() == null) {
          statement.bindNull(3);
        } else {
          statement.bindLong(3, entity.getSetId());
        }
        if (entity.getItemId() == null) {
          statement.bindNull(4);
        } else {
          statement.bindLong(4, entity.getItemId());
        }
        if (entity.getItemType() == null) {
          statement.bindNull(5);
        } else {
          statement.bindText(5, entity.getItemType());
        }
        if (entity.getSerialNumber() == null) {
          statement.bindNull(6);
        } else {
          statement.bindText(6, entity.getSerialNumber());
        }
        if (entity.getModelNumber() == null) {
          statement.bindNull(7);
        } else {
          statement.bindText(7, entity.getModelNumber());
        }
        if (entity.getPreviousStatus() == null) {
          statement.bindNull(8);
        } else {
          statement.bindText(8, entity.getPreviousStatus());
        }
        if (entity.getNewStatus() == null) {
          statement.bindNull(9);
        } else {
          statement.bindText(9, entity.getNewStatus());
        }
        if (entity.getRemark() == null) {
          statement.bindNull(10);
        } else {
          statement.bindText(10, entity.getRemark());
        }
        if (entity.getUpdatedByUserId() == null) {
          statement.bindNull(11);
        } else {
          statement.bindText(11, entity.getUpdatedByUserId());
        }
        if (entity.getUpdatedByName() == null) {
          statement.bindNull(12);
        } else {
          statement.bindText(12, entity.getUpdatedByName());
        }
        statement.bindLong(13, entity.getCreatedAt());
        statement.bindLong(14, entity.getRetryCount());
        final String _tmp = Converters.fromSyncStatus(entity.getSyncStatus());
        if (_tmp == null) {
          statement.bindNull(15);
        } else {
          statement.bindText(15, _tmp);
        }
        if (entity.getLastError() == null) {
          statement.bindNull(16);
        } else {
          statement.bindText(16, entity.getLastError());
        }
        if (entity.getSyncedAt() == null) {
          statement.bindNull(17);
        } else {
          statement.bindLong(17, entity.getSyncedAt());
        }
      }
    };
    this.__deleteAdapterOfPendingUpdateEntity = new EntityDeleteOrUpdateAdapter<PendingUpdateEntity>() {
      @Override
      @NonNull
      protected String createQuery() {
        return "DELETE FROM `pending_updates` WHERE `id` = ?";
      }

      @Override
      protected void bind(@NonNull final SQLiteStatement statement,
          @NonNull final PendingUpdateEntity entity) {
        if (entity.getId() == null) {
          statement.bindNull(1);
        } else {
          statement.bindText(1, entity.getId());
        }
      }
    };
    this.__updateAdapterOfPendingUpdateEntity = new EntityDeleteOrUpdateAdapter<PendingUpdateEntity>() {
      @Override
      @NonNull
      protected String createQuery() {
        return "UPDATE OR ABORT `pending_updates` SET `id` = ?,`set_code` = ?,`set_id` = ?,`item_id` = ?,`item_type` = ?,`serial_number` = ?,`model_number` = ?,`previous_status` = ?,`new_status` = ?,`remark` = ?,`updated_by_user_id` = ?,`updated_by_name` = ?,`created_at` = ?,`retry_count` = ?,`sync_status` = ?,`last_error` = ?,`synced_at` = ? WHERE `id` = ?";
      }

      @Override
      protected void bind(@NonNull final SQLiteStatement statement,
          @NonNull final PendingUpdateEntity entity) {
        if (entity.getId() == null) {
          statement.bindNull(1);
        } else {
          statement.bindText(1, entity.getId());
        }
        if (entity.getSetCode() == null) {
          statement.bindNull(2);
        } else {
          statement.bindText(2, entity.getSetCode());
        }
        if (entity.getSetId() == null) {
          statement.bindNull(3);
        } else {
          statement.bindLong(3, entity.getSetId());
        }
        if (entity.getItemId() == null) {
          statement.bindNull(4);
        } else {
          statement.bindLong(4, entity.getItemId());
        }
        if (entity.getItemType() == null) {
          statement.bindNull(5);
        } else {
          statement.bindText(5, entity.getItemType());
        }
        if (entity.getSerialNumber() == null) {
          statement.bindNull(6);
        } else {
          statement.bindText(6, entity.getSerialNumber());
        }
        if (entity.getModelNumber() == null) {
          statement.bindNull(7);
        } else {
          statement.bindText(7, entity.getModelNumber());
        }
        if (entity.getPreviousStatus() == null) {
          statement.bindNull(8);
        } else {
          statement.bindText(8, entity.getPreviousStatus());
        }
        if (entity.getNewStatus() == null) {
          statement.bindNull(9);
        } else {
          statement.bindText(9, entity.getNewStatus());
        }
        if (entity.getRemark() == null) {
          statement.bindNull(10);
        } else {
          statement.bindText(10, entity.getRemark());
        }
        if (entity.getUpdatedByUserId() == null) {
          statement.bindNull(11);
        } else {
          statement.bindText(11, entity.getUpdatedByUserId());
        }
        if (entity.getUpdatedByName() == null) {
          statement.bindNull(12);
        } else {
          statement.bindText(12, entity.getUpdatedByName());
        }
        statement.bindLong(13, entity.getCreatedAt());
        statement.bindLong(14, entity.getRetryCount());
        final String _tmp = Converters.fromSyncStatus(entity.getSyncStatus());
        if (_tmp == null) {
          statement.bindNull(15);
        } else {
          statement.bindText(15, _tmp);
        }
        if (entity.getLastError() == null) {
          statement.bindNull(16);
        } else {
          statement.bindText(16, entity.getLastError());
        }
        if (entity.getSyncedAt() == null) {
          statement.bindNull(17);
        } else {
          statement.bindLong(17, entity.getSyncedAt());
        }
        if (entity.getId() == null) {
          statement.bindNull(18);
        } else {
          statement.bindText(18, entity.getId());
        }
      }
    };
  }

  @Override
  public Object insert(final PendingUpdateEntity entity,
      final Continuation<? super Long> $completion) {
    if (entity == null) throw new NullPointerException();
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      return __insertAdapterOfPendingUpdateEntity.insertAndReturnId(_connection, entity);
    }, $completion);
  }

  @Override
  public Object insertAll(final List<PendingUpdateEntity> entities,
      final Continuation<? super Unit> $completion) {
    if (entities == null) throw new NullPointerException();
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      __insertAdapterOfPendingUpdateEntity.insert(_connection, entities);
      return Unit.INSTANCE;
    }, $completion);
  }

  @Override
  public Object delete(final PendingUpdateEntity entity,
      final Continuation<? super Unit> $completion) {
    if (entity == null) throw new NullPointerException();
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      __deleteAdapterOfPendingUpdateEntity.handle(_connection, entity);
      return Unit.INSTANCE;
    }, $completion);
  }

  @Override
  public Object update(final PendingUpdateEntity entity,
      final Continuation<? super Unit> $completion) {
    if (entity == null) throw new NullPointerException();
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      __updateAdapterOfPendingUpdateEntity.handle(_connection, entity);
      return Unit.INSTANCE;
    }, $completion);
  }

  @Override
  public Object getByStatus(final SyncStatus status,
      final Continuation<? super List<PendingUpdateEntity>> $completion) {
    final String _sql = "SELECT * FROM pending_updates WHERE sync_status = ? ORDER BY created_at ASC";
    return DBUtil.performSuspending(__db, true, false, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        final String _tmp = Converters.fromSyncStatus(status);
        if (_tmp == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, _tmp);
        }
        final int _columnIndexOfId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "id");
        final int _columnIndexOfSetCode = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "set_code");
        final int _columnIndexOfSetId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "set_id");
        final int _columnIndexOfItemId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "item_id");
        final int _columnIndexOfItemType = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "item_type");
        final int _columnIndexOfSerialNumber = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "serial_number");
        final int _columnIndexOfModelNumber = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "model_number");
        final int _columnIndexOfPreviousStatus = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "previous_status");
        final int _columnIndexOfNewStatus = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "new_status");
        final int _columnIndexOfRemark = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "remark");
        final int _columnIndexOfUpdatedByUserId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "updated_by_user_id");
        final int _columnIndexOfUpdatedByName = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "updated_by_name");
        final int _columnIndexOfCreatedAt = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "created_at");
        final int _columnIndexOfRetryCount = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "retry_count");
        final int _columnIndexOfSyncStatus = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "sync_status");
        final int _columnIndexOfLastError = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "last_error");
        final int _columnIndexOfSyncedAt = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "synced_at");
        final List<PendingUpdateEntity> _result = new ArrayList<PendingUpdateEntity>();
        while (_stmt.step()) {
          final PendingUpdateEntity _item;
          final String _tmpId;
          if (_stmt.isNull(_columnIndexOfId)) {
            _tmpId = null;
          } else {
            _tmpId = _stmt.getText(_columnIndexOfId);
          }
          final String _tmpSetCode;
          if (_stmt.isNull(_columnIndexOfSetCode)) {
            _tmpSetCode = null;
          } else {
            _tmpSetCode = _stmt.getText(_columnIndexOfSetCode);
          }
          final Integer _tmpSetId;
          if (_stmt.isNull(_columnIndexOfSetId)) {
            _tmpSetId = null;
          } else {
            _tmpSetId = (int) (_stmt.getLong(_columnIndexOfSetId));
          }
          final Integer _tmpItemId;
          if (_stmt.isNull(_columnIndexOfItemId)) {
            _tmpItemId = null;
          } else {
            _tmpItemId = (int) (_stmt.getLong(_columnIndexOfItemId));
          }
          final String _tmpItemType;
          if (_stmt.isNull(_columnIndexOfItemType)) {
            _tmpItemType = null;
          } else {
            _tmpItemType = _stmt.getText(_columnIndexOfItemType);
          }
          final String _tmpSerialNumber;
          if (_stmt.isNull(_columnIndexOfSerialNumber)) {
            _tmpSerialNumber = null;
          } else {
            _tmpSerialNumber = _stmt.getText(_columnIndexOfSerialNumber);
          }
          final String _tmpModelNumber;
          if (_stmt.isNull(_columnIndexOfModelNumber)) {
            _tmpModelNumber = null;
          } else {
            _tmpModelNumber = _stmt.getText(_columnIndexOfModelNumber);
          }
          final String _tmpPreviousStatus;
          if (_stmt.isNull(_columnIndexOfPreviousStatus)) {
            _tmpPreviousStatus = null;
          } else {
            _tmpPreviousStatus = _stmt.getText(_columnIndexOfPreviousStatus);
          }
          final String _tmpNewStatus;
          if (_stmt.isNull(_columnIndexOfNewStatus)) {
            _tmpNewStatus = null;
          } else {
            _tmpNewStatus = _stmt.getText(_columnIndexOfNewStatus);
          }
          final String _tmpRemark;
          if (_stmt.isNull(_columnIndexOfRemark)) {
            _tmpRemark = null;
          } else {
            _tmpRemark = _stmt.getText(_columnIndexOfRemark);
          }
          final String _tmpUpdatedByUserId;
          if (_stmt.isNull(_columnIndexOfUpdatedByUserId)) {
            _tmpUpdatedByUserId = null;
          } else {
            _tmpUpdatedByUserId = _stmt.getText(_columnIndexOfUpdatedByUserId);
          }
          final String _tmpUpdatedByName;
          if (_stmt.isNull(_columnIndexOfUpdatedByName)) {
            _tmpUpdatedByName = null;
          } else {
            _tmpUpdatedByName = _stmt.getText(_columnIndexOfUpdatedByName);
          }
          final long _tmpCreatedAt;
          _tmpCreatedAt = _stmt.getLong(_columnIndexOfCreatedAt);
          final int _tmpRetryCount;
          _tmpRetryCount = (int) (_stmt.getLong(_columnIndexOfRetryCount));
          final SyncStatus _tmpSyncStatus;
          final String _tmp_1;
          if (_stmt.isNull(_columnIndexOfSyncStatus)) {
            _tmp_1 = null;
          } else {
            _tmp_1 = _stmt.getText(_columnIndexOfSyncStatus);
          }
          _tmpSyncStatus = Converters.toSyncStatus(_tmp_1);
          final String _tmpLastError;
          if (_stmt.isNull(_columnIndexOfLastError)) {
            _tmpLastError = null;
          } else {
            _tmpLastError = _stmt.getText(_columnIndexOfLastError);
          }
          final Long _tmpSyncedAt;
          if (_stmt.isNull(_columnIndexOfSyncedAt)) {
            _tmpSyncedAt = null;
          } else {
            _tmpSyncedAt = _stmt.getLong(_columnIndexOfSyncedAt);
          }
          _item = new PendingUpdateEntity(_tmpId,_tmpSetCode,_tmpSetId,_tmpItemId,_tmpItemType,_tmpSerialNumber,_tmpModelNumber,_tmpPreviousStatus,_tmpNewStatus,_tmpRemark,_tmpUpdatedByUserId,_tmpUpdatedByName,_tmpCreatedAt,_tmpRetryCount,_tmpSyncStatus,_tmpLastError,_tmpSyncedAt);
          _result.add(_item);
        }
        return _result;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Object getPendingOrFailed(
      final Continuation<? super List<PendingUpdateEntity>> $completion) {
    final String _sql = "SELECT `pending_updates`.`id` AS `id`, `pending_updates`.`set_code` AS `set_code`, `pending_updates`.`set_id` AS `set_id`, `pending_updates`.`item_id` AS `item_id`, `pending_updates`.`item_type` AS `item_type`, `pending_updates`.`serial_number` AS `serial_number`, `pending_updates`.`model_number` AS `model_number`, `pending_updates`.`previous_status` AS `previous_status`, `pending_updates`.`new_status` AS `new_status`, `pending_updates`.`remark` AS `remark`, `pending_updates`.`updated_by_user_id` AS `updated_by_user_id`, `pending_updates`.`updated_by_name` AS `updated_by_name`, `pending_updates`.`created_at` AS `created_at`, `pending_updates`.`retry_count` AS `retry_count`, `pending_updates`.`sync_status` AS `sync_status`, `pending_updates`.`last_error` AS `last_error`, `pending_updates`.`synced_at` AS `synced_at` FROM pending_updates WHERE sync_status IN ('PENDING', 'SYNCING', 'FAILED') ORDER BY created_at ASC";
    return DBUtil.performSuspending(__db, true, false, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        final int _columnIndexOfId = 0;
        final int _columnIndexOfSetCode = 1;
        final int _columnIndexOfSetId = 2;
        final int _columnIndexOfItemId = 3;
        final int _columnIndexOfItemType = 4;
        final int _columnIndexOfSerialNumber = 5;
        final int _columnIndexOfModelNumber = 6;
        final int _columnIndexOfPreviousStatus = 7;
        final int _columnIndexOfNewStatus = 8;
        final int _columnIndexOfRemark = 9;
        final int _columnIndexOfUpdatedByUserId = 10;
        final int _columnIndexOfUpdatedByName = 11;
        final int _columnIndexOfCreatedAt = 12;
        final int _columnIndexOfRetryCount = 13;
        final int _columnIndexOfSyncStatus = 14;
        final int _columnIndexOfLastError = 15;
        final int _columnIndexOfSyncedAt = 16;
        final List<PendingUpdateEntity> _result = new ArrayList<PendingUpdateEntity>();
        while (_stmt.step()) {
          final PendingUpdateEntity _item;
          final String _tmpId;
          if (_stmt.isNull(_columnIndexOfId)) {
            _tmpId = null;
          } else {
            _tmpId = _stmt.getText(_columnIndexOfId);
          }
          final String _tmpSetCode;
          if (_stmt.isNull(_columnIndexOfSetCode)) {
            _tmpSetCode = null;
          } else {
            _tmpSetCode = _stmt.getText(_columnIndexOfSetCode);
          }
          final Integer _tmpSetId;
          if (_stmt.isNull(_columnIndexOfSetId)) {
            _tmpSetId = null;
          } else {
            _tmpSetId = (int) (_stmt.getLong(_columnIndexOfSetId));
          }
          final Integer _tmpItemId;
          if (_stmt.isNull(_columnIndexOfItemId)) {
            _tmpItemId = null;
          } else {
            _tmpItemId = (int) (_stmt.getLong(_columnIndexOfItemId));
          }
          final String _tmpItemType;
          if (_stmt.isNull(_columnIndexOfItemType)) {
            _tmpItemType = null;
          } else {
            _tmpItemType = _stmt.getText(_columnIndexOfItemType);
          }
          final String _tmpSerialNumber;
          if (_stmt.isNull(_columnIndexOfSerialNumber)) {
            _tmpSerialNumber = null;
          } else {
            _tmpSerialNumber = _stmt.getText(_columnIndexOfSerialNumber);
          }
          final String _tmpModelNumber;
          if (_stmt.isNull(_columnIndexOfModelNumber)) {
            _tmpModelNumber = null;
          } else {
            _tmpModelNumber = _stmt.getText(_columnIndexOfModelNumber);
          }
          final String _tmpPreviousStatus;
          if (_stmt.isNull(_columnIndexOfPreviousStatus)) {
            _tmpPreviousStatus = null;
          } else {
            _tmpPreviousStatus = _stmt.getText(_columnIndexOfPreviousStatus);
          }
          final String _tmpNewStatus;
          if (_stmt.isNull(_columnIndexOfNewStatus)) {
            _tmpNewStatus = null;
          } else {
            _tmpNewStatus = _stmt.getText(_columnIndexOfNewStatus);
          }
          final String _tmpRemark;
          if (_stmt.isNull(_columnIndexOfRemark)) {
            _tmpRemark = null;
          } else {
            _tmpRemark = _stmt.getText(_columnIndexOfRemark);
          }
          final String _tmpUpdatedByUserId;
          if (_stmt.isNull(_columnIndexOfUpdatedByUserId)) {
            _tmpUpdatedByUserId = null;
          } else {
            _tmpUpdatedByUserId = _stmt.getText(_columnIndexOfUpdatedByUserId);
          }
          final String _tmpUpdatedByName;
          if (_stmt.isNull(_columnIndexOfUpdatedByName)) {
            _tmpUpdatedByName = null;
          } else {
            _tmpUpdatedByName = _stmt.getText(_columnIndexOfUpdatedByName);
          }
          final long _tmpCreatedAt;
          _tmpCreatedAt = _stmt.getLong(_columnIndexOfCreatedAt);
          final int _tmpRetryCount;
          _tmpRetryCount = (int) (_stmt.getLong(_columnIndexOfRetryCount));
          final SyncStatus _tmpSyncStatus;
          final String _tmp;
          if (_stmt.isNull(_columnIndexOfSyncStatus)) {
            _tmp = null;
          } else {
            _tmp = _stmt.getText(_columnIndexOfSyncStatus);
          }
          _tmpSyncStatus = Converters.toSyncStatus(_tmp);
          final String _tmpLastError;
          if (_stmt.isNull(_columnIndexOfLastError)) {
            _tmpLastError = null;
          } else {
            _tmpLastError = _stmt.getText(_columnIndexOfLastError);
          }
          final Long _tmpSyncedAt;
          if (_stmt.isNull(_columnIndexOfSyncedAt)) {
            _tmpSyncedAt = null;
          } else {
            _tmpSyncedAt = _stmt.getLong(_columnIndexOfSyncedAt);
          }
          _item = new PendingUpdateEntity(_tmpId,_tmpSetCode,_tmpSetId,_tmpItemId,_tmpItemType,_tmpSerialNumber,_tmpModelNumber,_tmpPreviousStatus,_tmpNewStatus,_tmpRemark,_tmpUpdatedByUserId,_tmpUpdatedByName,_tmpCreatedAt,_tmpRetryCount,_tmpSyncStatus,_tmpLastError,_tmpSyncedAt);
          _result.add(_item);
        }
        return _result;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Object getBySetCode(final String setCode,
      final Continuation<? super List<PendingUpdateEntity>> $completion) {
    final String _sql = "SELECT * FROM pending_updates WHERE set_code = ? ORDER BY created_at DESC";
    return DBUtil.performSuspending(__db, true, false, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        if (setCode == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, setCode);
        }
        final int _columnIndexOfId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "id");
        final int _columnIndexOfSetCode = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "set_code");
        final int _columnIndexOfSetId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "set_id");
        final int _columnIndexOfItemId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "item_id");
        final int _columnIndexOfItemType = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "item_type");
        final int _columnIndexOfSerialNumber = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "serial_number");
        final int _columnIndexOfModelNumber = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "model_number");
        final int _columnIndexOfPreviousStatus = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "previous_status");
        final int _columnIndexOfNewStatus = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "new_status");
        final int _columnIndexOfRemark = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "remark");
        final int _columnIndexOfUpdatedByUserId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "updated_by_user_id");
        final int _columnIndexOfUpdatedByName = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "updated_by_name");
        final int _columnIndexOfCreatedAt = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "created_at");
        final int _columnIndexOfRetryCount = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "retry_count");
        final int _columnIndexOfSyncStatus = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "sync_status");
        final int _columnIndexOfLastError = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "last_error");
        final int _columnIndexOfSyncedAt = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "synced_at");
        final List<PendingUpdateEntity> _result = new ArrayList<PendingUpdateEntity>();
        while (_stmt.step()) {
          final PendingUpdateEntity _item;
          final String _tmpId;
          if (_stmt.isNull(_columnIndexOfId)) {
            _tmpId = null;
          } else {
            _tmpId = _stmt.getText(_columnIndexOfId);
          }
          final String _tmpSetCode;
          if (_stmt.isNull(_columnIndexOfSetCode)) {
            _tmpSetCode = null;
          } else {
            _tmpSetCode = _stmt.getText(_columnIndexOfSetCode);
          }
          final Integer _tmpSetId;
          if (_stmt.isNull(_columnIndexOfSetId)) {
            _tmpSetId = null;
          } else {
            _tmpSetId = (int) (_stmt.getLong(_columnIndexOfSetId));
          }
          final Integer _tmpItemId;
          if (_stmt.isNull(_columnIndexOfItemId)) {
            _tmpItemId = null;
          } else {
            _tmpItemId = (int) (_stmt.getLong(_columnIndexOfItemId));
          }
          final String _tmpItemType;
          if (_stmt.isNull(_columnIndexOfItemType)) {
            _tmpItemType = null;
          } else {
            _tmpItemType = _stmt.getText(_columnIndexOfItemType);
          }
          final String _tmpSerialNumber;
          if (_stmt.isNull(_columnIndexOfSerialNumber)) {
            _tmpSerialNumber = null;
          } else {
            _tmpSerialNumber = _stmt.getText(_columnIndexOfSerialNumber);
          }
          final String _tmpModelNumber;
          if (_stmt.isNull(_columnIndexOfModelNumber)) {
            _tmpModelNumber = null;
          } else {
            _tmpModelNumber = _stmt.getText(_columnIndexOfModelNumber);
          }
          final String _tmpPreviousStatus;
          if (_stmt.isNull(_columnIndexOfPreviousStatus)) {
            _tmpPreviousStatus = null;
          } else {
            _tmpPreviousStatus = _stmt.getText(_columnIndexOfPreviousStatus);
          }
          final String _tmpNewStatus;
          if (_stmt.isNull(_columnIndexOfNewStatus)) {
            _tmpNewStatus = null;
          } else {
            _tmpNewStatus = _stmt.getText(_columnIndexOfNewStatus);
          }
          final String _tmpRemark;
          if (_stmt.isNull(_columnIndexOfRemark)) {
            _tmpRemark = null;
          } else {
            _tmpRemark = _stmt.getText(_columnIndexOfRemark);
          }
          final String _tmpUpdatedByUserId;
          if (_stmt.isNull(_columnIndexOfUpdatedByUserId)) {
            _tmpUpdatedByUserId = null;
          } else {
            _tmpUpdatedByUserId = _stmt.getText(_columnIndexOfUpdatedByUserId);
          }
          final String _tmpUpdatedByName;
          if (_stmt.isNull(_columnIndexOfUpdatedByName)) {
            _tmpUpdatedByName = null;
          } else {
            _tmpUpdatedByName = _stmt.getText(_columnIndexOfUpdatedByName);
          }
          final long _tmpCreatedAt;
          _tmpCreatedAt = _stmt.getLong(_columnIndexOfCreatedAt);
          final int _tmpRetryCount;
          _tmpRetryCount = (int) (_stmt.getLong(_columnIndexOfRetryCount));
          final SyncStatus _tmpSyncStatus;
          final String _tmp;
          if (_stmt.isNull(_columnIndexOfSyncStatus)) {
            _tmp = null;
          } else {
            _tmp = _stmt.getText(_columnIndexOfSyncStatus);
          }
          _tmpSyncStatus = Converters.toSyncStatus(_tmp);
          final String _tmpLastError;
          if (_stmt.isNull(_columnIndexOfLastError)) {
            _tmpLastError = null;
          } else {
            _tmpLastError = _stmt.getText(_columnIndexOfLastError);
          }
          final Long _tmpSyncedAt;
          if (_stmt.isNull(_columnIndexOfSyncedAt)) {
            _tmpSyncedAt = null;
          } else {
            _tmpSyncedAt = _stmt.getLong(_columnIndexOfSyncedAt);
          }
          _item = new PendingUpdateEntity(_tmpId,_tmpSetCode,_tmpSetId,_tmpItemId,_tmpItemType,_tmpSerialNumber,_tmpModelNumber,_tmpPreviousStatus,_tmpNewStatus,_tmpRemark,_tmpUpdatedByUserId,_tmpUpdatedByName,_tmpCreatedAt,_tmpRetryCount,_tmpSyncStatus,_tmpLastError,_tmpSyncedAt);
          _result.add(_item);
        }
        return _result;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Flow<Integer> getPendingCountFlow() {
    final String _sql = "SELECT COUNT(*) FROM pending_updates WHERE sync_status IN ('PENDING', 'SYNCING', 'FAILED')";
    return FlowUtil.createFlow(__db, false, new String[] {"pending_updates"}, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        final Integer _result;
        if (_stmt.step()) {
          final Integer _tmp;
          if (_stmt.isNull(0)) {
            _tmp = null;
          } else {
            _tmp = (int) (_stmt.getLong(0));
          }
          _result = _tmp;
        } else {
          _result = null;
        }
        return _result;
      } finally {
        _stmt.close();
      }
    });
  }

  @Override
  public Object getPendingCount(final Continuation<? super Integer> $completion) {
    final String _sql = "SELECT COUNT(*) FROM pending_updates WHERE sync_status IN ('PENDING', 'SYNCING', 'FAILED')";
    return DBUtil.performSuspending(__db, true, false, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        final Integer _result;
        if (_stmt.step()) {
          final Integer _tmp;
          if (_stmt.isNull(0)) {
            _tmp = null;
          } else {
            _tmp = (int) (_stmt.getLong(0));
          }
          _result = _tmp;
        } else {
          _result = null;
        }
        return _result;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Object getAll(final Continuation<? super List<PendingUpdateEntity>> $completion) {
    final String _sql = "SELECT `pending_updates`.`id` AS `id`, `pending_updates`.`set_code` AS `set_code`, `pending_updates`.`set_id` AS `set_id`, `pending_updates`.`item_id` AS `item_id`, `pending_updates`.`item_type` AS `item_type`, `pending_updates`.`serial_number` AS `serial_number`, `pending_updates`.`model_number` AS `model_number`, `pending_updates`.`previous_status` AS `previous_status`, `pending_updates`.`new_status` AS `new_status`, `pending_updates`.`remark` AS `remark`, `pending_updates`.`updated_by_user_id` AS `updated_by_user_id`, `pending_updates`.`updated_by_name` AS `updated_by_name`, `pending_updates`.`created_at` AS `created_at`, `pending_updates`.`retry_count` AS `retry_count`, `pending_updates`.`sync_status` AS `sync_status`, `pending_updates`.`last_error` AS `last_error`, `pending_updates`.`synced_at` AS `synced_at` FROM pending_updates";
    return DBUtil.performSuspending(__db, true, false, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        final int _columnIndexOfId = 0;
        final int _columnIndexOfSetCode = 1;
        final int _columnIndexOfSetId = 2;
        final int _columnIndexOfItemId = 3;
        final int _columnIndexOfItemType = 4;
        final int _columnIndexOfSerialNumber = 5;
        final int _columnIndexOfModelNumber = 6;
        final int _columnIndexOfPreviousStatus = 7;
        final int _columnIndexOfNewStatus = 8;
        final int _columnIndexOfRemark = 9;
        final int _columnIndexOfUpdatedByUserId = 10;
        final int _columnIndexOfUpdatedByName = 11;
        final int _columnIndexOfCreatedAt = 12;
        final int _columnIndexOfRetryCount = 13;
        final int _columnIndexOfSyncStatus = 14;
        final int _columnIndexOfLastError = 15;
        final int _columnIndexOfSyncedAt = 16;
        final List<PendingUpdateEntity> _result = new ArrayList<PendingUpdateEntity>();
        while (_stmt.step()) {
          final PendingUpdateEntity _item;
          final String _tmpId;
          if (_stmt.isNull(_columnIndexOfId)) {
            _tmpId = null;
          } else {
            _tmpId = _stmt.getText(_columnIndexOfId);
          }
          final String _tmpSetCode;
          if (_stmt.isNull(_columnIndexOfSetCode)) {
            _tmpSetCode = null;
          } else {
            _tmpSetCode = _stmt.getText(_columnIndexOfSetCode);
          }
          final Integer _tmpSetId;
          if (_stmt.isNull(_columnIndexOfSetId)) {
            _tmpSetId = null;
          } else {
            _tmpSetId = (int) (_stmt.getLong(_columnIndexOfSetId));
          }
          final Integer _tmpItemId;
          if (_stmt.isNull(_columnIndexOfItemId)) {
            _tmpItemId = null;
          } else {
            _tmpItemId = (int) (_stmt.getLong(_columnIndexOfItemId));
          }
          final String _tmpItemType;
          if (_stmt.isNull(_columnIndexOfItemType)) {
            _tmpItemType = null;
          } else {
            _tmpItemType = _stmt.getText(_columnIndexOfItemType);
          }
          final String _tmpSerialNumber;
          if (_stmt.isNull(_columnIndexOfSerialNumber)) {
            _tmpSerialNumber = null;
          } else {
            _tmpSerialNumber = _stmt.getText(_columnIndexOfSerialNumber);
          }
          final String _tmpModelNumber;
          if (_stmt.isNull(_columnIndexOfModelNumber)) {
            _tmpModelNumber = null;
          } else {
            _tmpModelNumber = _stmt.getText(_columnIndexOfModelNumber);
          }
          final String _tmpPreviousStatus;
          if (_stmt.isNull(_columnIndexOfPreviousStatus)) {
            _tmpPreviousStatus = null;
          } else {
            _tmpPreviousStatus = _stmt.getText(_columnIndexOfPreviousStatus);
          }
          final String _tmpNewStatus;
          if (_stmt.isNull(_columnIndexOfNewStatus)) {
            _tmpNewStatus = null;
          } else {
            _tmpNewStatus = _stmt.getText(_columnIndexOfNewStatus);
          }
          final String _tmpRemark;
          if (_stmt.isNull(_columnIndexOfRemark)) {
            _tmpRemark = null;
          } else {
            _tmpRemark = _stmt.getText(_columnIndexOfRemark);
          }
          final String _tmpUpdatedByUserId;
          if (_stmt.isNull(_columnIndexOfUpdatedByUserId)) {
            _tmpUpdatedByUserId = null;
          } else {
            _tmpUpdatedByUserId = _stmt.getText(_columnIndexOfUpdatedByUserId);
          }
          final String _tmpUpdatedByName;
          if (_stmt.isNull(_columnIndexOfUpdatedByName)) {
            _tmpUpdatedByName = null;
          } else {
            _tmpUpdatedByName = _stmt.getText(_columnIndexOfUpdatedByName);
          }
          final long _tmpCreatedAt;
          _tmpCreatedAt = _stmt.getLong(_columnIndexOfCreatedAt);
          final int _tmpRetryCount;
          _tmpRetryCount = (int) (_stmt.getLong(_columnIndexOfRetryCount));
          final SyncStatus _tmpSyncStatus;
          final String _tmp;
          if (_stmt.isNull(_columnIndexOfSyncStatus)) {
            _tmp = null;
          } else {
            _tmp = _stmt.getText(_columnIndexOfSyncStatus);
          }
          _tmpSyncStatus = Converters.toSyncStatus(_tmp);
          final String _tmpLastError;
          if (_stmt.isNull(_columnIndexOfLastError)) {
            _tmpLastError = null;
          } else {
            _tmpLastError = _stmt.getText(_columnIndexOfLastError);
          }
          final Long _tmpSyncedAt;
          if (_stmt.isNull(_columnIndexOfSyncedAt)) {
            _tmpSyncedAt = null;
          } else {
            _tmpSyncedAt = _stmt.getLong(_columnIndexOfSyncedAt);
          }
          _item = new PendingUpdateEntity(_tmpId,_tmpSetCode,_tmpSetId,_tmpItemId,_tmpItemType,_tmpSerialNumber,_tmpModelNumber,_tmpPreviousStatus,_tmpNewStatus,_tmpRemark,_tmpUpdatedByUserId,_tmpUpdatedByName,_tmpCreatedAt,_tmpRetryCount,_tmpSyncStatus,_tmpLastError,_tmpSyncedAt);
          _result.add(_item);
        }
        return _result;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Object deleteById(final String id, final Continuation<? super Unit> $completion) {
    final String _sql = "DELETE FROM pending_updates WHERE id = ?";
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        if (id == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, id);
        }
        _stmt.step();
        return Unit.INSTANCE;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Object deleteSynced(final Continuation<? super Unit> $completion) {
    final String _sql = "DELETE FROM pending_updates WHERE sync_status = 'SYNCED'";
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        _stmt.step();
        return Unit.INSTANCE;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Object markSyncing(final String id, final Continuation<? super Unit> $completion) {
    final String _sql = "UPDATE pending_updates SET sync_status = 'SYNCING' WHERE id = ?";
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        if (id == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, id);
        }
        _stmt.step();
        return Unit.INSTANCE;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Object markFailed(final String id, final String error, final SyncStatus status,
      final Continuation<? super Unit> $completion) {
    final String _sql = "UPDATE pending_updates SET retry_count = retry_count + 1, last_error = ?, sync_status = ? WHERE id = ?";
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        if (error == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, error);
        }
        _argIndex = 2;
        final String _tmp = Converters.fromSyncStatus(status);
        if (_tmp == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, _tmp);
        }
        _argIndex = 3;
        if (id == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, id);
        }
        _stmt.step();
        return Unit.INSTANCE;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Object markSynced(final String id, final long timestamp,
      final Continuation<? super Unit> $completion) {
    final String _sql = "UPDATE pending_updates SET sync_status = 'SYNCED', synced_at = ? WHERE id = ?";
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        _stmt.bindLong(_argIndex, timestamp);
        _argIndex = 2;
        if (id == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, id);
        }
        _stmt.step();
        return Unit.INSTANCE;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @NonNull
  public static List<Class<?>> getRequiredConverters() {
    return Collections.emptyList();
  }
}
