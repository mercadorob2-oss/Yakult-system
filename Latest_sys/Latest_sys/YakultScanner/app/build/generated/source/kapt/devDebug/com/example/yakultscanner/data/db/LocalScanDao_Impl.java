package com.example.yakultscanner.data.db;

import androidx.annotation.NonNull;
import androidx.room.EntityInsertAdapter;
import androidx.room.RoomDatabase;
import androidx.room.coroutines.FlowUtil;
import androidx.room.util.DBUtil;
import androidx.room.util.SQLiteStatementUtil;
import androidx.sqlite.SQLiteStatement;
import java.lang.Class;
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
public final class LocalScanDao_Impl implements LocalScanDao {
  private final RoomDatabase __db;

  private final EntityInsertAdapter<LocalScanSessionEntity> __insertAdapterOfLocalScanSessionEntity;

  private final EntityInsertAdapter<LocalScanItemEntity> __insertAdapterOfLocalScanItemEntity;

  public LocalScanDao_Impl(@NonNull final RoomDatabase __db) {
    this.__db = __db;
    this.__insertAdapterOfLocalScanSessionEntity = new EntityInsertAdapter<LocalScanSessionEntity>() {
      @Override
      @NonNull
      protected String createQuery() {
        return "INSERT OR REPLACE INTO `local_scan_sessions` (`id`,`title`,`created_by`,`created_at`,`updated_at`,`notes`,`status`,`sent_at`) VALUES (?,?,?,?,?,?,?,?)";
      }

      @Override
      protected void bind(@NonNull final SQLiteStatement statement,
          @NonNull final LocalScanSessionEntity entity) {
        if (entity.getId() == null) {
          statement.bindNull(1);
        } else {
          statement.bindText(1, entity.getId());
        }
        if (entity.getTitle() == null) {
          statement.bindNull(2);
        } else {
          statement.bindText(2, entity.getTitle());
        }
        if (entity.getCreatedBy() == null) {
          statement.bindNull(3);
        } else {
          statement.bindText(3, entity.getCreatedBy());
        }
        statement.bindLong(4, entity.getCreatedAt());
        statement.bindLong(5, entity.getUpdatedAt());
        if (entity.getNotes() == null) {
          statement.bindNull(6);
        } else {
          statement.bindText(6, entity.getNotes());
        }
        final String _tmp = Converters.fromLocalScanSessionStatus(entity.getStatus());
        if (_tmp == null) {
          statement.bindNull(7);
        } else {
          statement.bindText(7, _tmp);
        }
        if (entity.getSentAt() == null) {
          statement.bindNull(8);
        } else {
          statement.bindLong(8, entity.getSentAt());
        }
      }
    };
    this.__insertAdapterOfLocalScanItemEntity = new EntityInsertAdapter<LocalScanItemEntity>() {
      @Override
      @NonNull
      protected String createQuery() {
        return "INSERT OR REPLACE INTO `local_scan_items` (`id`,`session_id`,`row_number`,`serial_number`,`cell_phone_number`,`imei1`,`imei2`,`source`,`created_at`,`sent`,`sent_at`,`last_error`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)";
      }

      @Override
      protected void bind(@NonNull final SQLiteStatement statement,
          @NonNull final LocalScanItemEntity entity) {
        if (entity.getId() == null) {
          statement.bindNull(1);
        } else {
          statement.bindText(1, entity.getId());
        }
        if (entity.getSessionId() == null) {
          statement.bindNull(2);
        } else {
          statement.bindText(2, entity.getSessionId());
        }
        statement.bindLong(3, entity.getRowNumber());
        if (entity.getSerialNumber() == null) {
          statement.bindNull(4);
        } else {
          statement.bindText(4, entity.getSerialNumber());
        }
        if (entity.getCellPhoneNumber() == null) {
          statement.bindNull(5);
        } else {
          statement.bindText(5, entity.getCellPhoneNumber());
        }
        if (entity.getImei1() == null) {
          statement.bindNull(6);
        } else {
          statement.bindText(6, entity.getImei1());
        }
        if (entity.getImei2() == null) {
          statement.bindNull(7);
        } else {
          statement.bindText(7, entity.getImei2());
        }
        if (entity.getSource() == null) {
          statement.bindNull(8);
        } else {
          statement.bindText(8, entity.getSource());
        }
        statement.bindLong(9, entity.getCreatedAt());
        final int _tmp = entity.getSent() ? 1 : 0;
        statement.bindLong(10, _tmp);
        if (entity.getSentAt() == null) {
          statement.bindNull(11);
        } else {
          statement.bindLong(11, entity.getSentAt());
        }
        if (entity.getLastError() == null) {
          statement.bindNull(12);
        } else {
          statement.bindText(12, entity.getLastError());
        }
      }
    };
  }

  @Override
  public Object insertSession(final LocalScanSessionEntity session,
      final Continuation<? super Unit> $completion) {
    if (session == null) throw new NullPointerException();
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      __insertAdapterOfLocalScanSessionEntity.insert(_connection, session);
      return Unit.INSTANCE;
    }, $completion);
  }

  @Override
  public Object insertItems(final List<LocalScanItemEntity> items,
      final Continuation<? super Unit> $completion) {
    if (items == null) throw new NullPointerException();
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      __insertAdapterOfLocalScanItemEntity.insert(_connection, items);
      return Unit.INSTANCE;
    }, $completion);
  }

  @Override
  public Object replaceSession(final LocalScanSessionEntity session,
      final List<LocalScanItemEntity> items, final Continuation<? super Unit> $completion) {
    return DBUtil.performInTransactionSuspending(__db, (_cont) -> {
      return LocalScanDao.super.replaceSession(session, items, _cont);
    }, $completion);
  }

  @Override
  public Flow<List<LocalScanSessionWithCount>> observeSessions() {
    final String _sql = "\n"
            + "        SELECT `s`.`id`, `s`.`title`, `s`.`created_by`, `s`.`created_at`, `s`.`updated_at`, `s`.`notes`, `s`.`status`, `s`.`sent_at`, COUNT(i.id) AS item_count,\n"
            + "               COALESCE(SUM(CASE WHEN i.sent = 1 THEN 1 ELSE 0 END), 0) AS sent_count\n"
            + "        FROM local_scan_sessions s\n"
            + "        LEFT JOIN local_scan_items i ON i.session_id = s.id\n"
            + "        GROUP BY s.id\n"
            + "        ORDER BY s.updated_at DESC\n"
            + "        ";
    return FlowUtil.createFlow(__db, false, new String[] {"local_scan_sessions",
        "local_scan_items"}, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        final int _columnIndexOfId = 0;
        final int _columnIndexOfTitle = 1;
        final int _columnIndexOfCreatedBy = 2;
        final int _columnIndexOfCreatedAt = 3;
        final int _columnIndexOfUpdatedAt = 4;
        final int _columnIndexOfNotes = 5;
        final int _columnIndexOfStatus = 6;
        final int _columnIndexOfSentAt = 7;
        final int _columnIndexOfItemCount = 8;
        final int _columnIndexOfSentCount = 9;
        final List<LocalScanSessionWithCount> _result = new ArrayList<LocalScanSessionWithCount>();
        while (_stmt.step()) {
          final LocalScanSessionWithCount _item;
          final String _tmpId;
          if (_stmt.isNull(_columnIndexOfId)) {
            _tmpId = null;
          } else {
            _tmpId = _stmt.getText(_columnIndexOfId);
          }
          final String _tmpTitle;
          if (_stmt.isNull(_columnIndexOfTitle)) {
            _tmpTitle = null;
          } else {
            _tmpTitle = _stmt.getText(_columnIndexOfTitle);
          }
          final String _tmpCreatedBy;
          if (_stmt.isNull(_columnIndexOfCreatedBy)) {
            _tmpCreatedBy = null;
          } else {
            _tmpCreatedBy = _stmt.getText(_columnIndexOfCreatedBy);
          }
          final long _tmpCreatedAt;
          _tmpCreatedAt = _stmt.getLong(_columnIndexOfCreatedAt);
          final long _tmpUpdatedAt;
          _tmpUpdatedAt = _stmt.getLong(_columnIndexOfUpdatedAt);
          final String _tmpNotes;
          if (_stmt.isNull(_columnIndexOfNotes)) {
            _tmpNotes = null;
          } else {
            _tmpNotes = _stmt.getText(_columnIndexOfNotes);
          }
          final LocalScanSessionStatus _tmpStatus;
          final String _tmp;
          if (_stmt.isNull(_columnIndexOfStatus)) {
            _tmp = null;
          } else {
            _tmp = _stmt.getText(_columnIndexOfStatus);
          }
          _tmpStatus = Converters.toLocalScanSessionStatus(_tmp);
          final Long _tmpSentAt;
          if (_stmt.isNull(_columnIndexOfSentAt)) {
            _tmpSentAt = null;
          } else {
            _tmpSentAt = _stmt.getLong(_columnIndexOfSentAt);
          }
          final int _tmpItemCount;
          _tmpItemCount = (int) (_stmt.getLong(_columnIndexOfItemCount));
          final int _tmpSentCount;
          _tmpSentCount = (int) (_stmt.getLong(_columnIndexOfSentCount));
          _item = new LocalScanSessionWithCount(_tmpId,_tmpTitle,_tmpCreatedBy,_tmpCreatedAt,_tmpUpdatedAt,_tmpNotes,_tmpStatus,_tmpSentAt,_tmpItemCount,_tmpSentCount);
          _result.add(_item);
        }
        return _result;
      } finally {
        _stmt.close();
      }
    });
  }

  @Override
  public Object getSession(final String sessionId,
      final Continuation<? super LocalScanSessionEntity> $completion) {
    final String _sql = "SELECT * FROM local_scan_sessions WHERE id = ? LIMIT 1";
    return DBUtil.performSuspending(__db, true, false, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        if (sessionId == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, sessionId);
        }
        final int _columnIndexOfId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "id");
        final int _columnIndexOfTitle = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "title");
        final int _columnIndexOfCreatedBy = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "created_by");
        final int _columnIndexOfCreatedAt = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "created_at");
        final int _columnIndexOfUpdatedAt = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "updated_at");
        final int _columnIndexOfNotes = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "notes");
        final int _columnIndexOfStatus = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "status");
        final int _columnIndexOfSentAt = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "sent_at");
        final LocalScanSessionEntity _result;
        if (_stmt.step()) {
          final String _tmpId;
          if (_stmt.isNull(_columnIndexOfId)) {
            _tmpId = null;
          } else {
            _tmpId = _stmt.getText(_columnIndexOfId);
          }
          final String _tmpTitle;
          if (_stmt.isNull(_columnIndexOfTitle)) {
            _tmpTitle = null;
          } else {
            _tmpTitle = _stmt.getText(_columnIndexOfTitle);
          }
          final String _tmpCreatedBy;
          if (_stmt.isNull(_columnIndexOfCreatedBy)) {
            _tmpCreatedBy = null;
          } else {
            _tmpCreatedBy = _stmt.getText(_columnIndexOfCreatedBy);
          }
          final long _tmpCreatedAt;
          _tmpCreatedAt = _stmt.getLong(_columnIndexOfCreatedAt);
          final long _tmpUpdatedAt;
          _tmpUpdatedAt = _stmt.getLong(_columnIndexOfUpdatedAt);
          final String _tmpNotes;
          if (_stmt.isNull(_columnIndexOfNotes)) {
            _tmpNotes = null;
          } else {
            _tmpNotes = _stmt.getText(_columnIndexOfNotes);
          }
          final LocalScanSessionStatus _tmpStatus;
          final String _tmp;
          if (_stmt.isNull(_columnIndexOfStatus)) {
            _tmp = null;
          } else {
            _tmp = _stmt.getText(_columnIndexOfStatus);
          }
          _tmpStatus = Converters.toLocalScanSessionStatus(_tmp);
          final Long _tmpSentAt;
          if (_stmt.isNull(_columnIndexOfSentAt)) {
            _tmpSentAt = null;
          } else {
            _tmpSentAt = _stmt.getLong(_columnIndexOfSentAt);
          }
          _result = new LocalScanSessionEntity(_tmpId,_tmpTitle,_tmpCreatedBy,_tmpCreatedAt,_tmpUpdatedAt,_tmpNotes,_tmpStatus,_tmpSentAt);
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
  public Object getItems(final String sessionId,
      final Continuation<? super List<LocalScanItemEntity>> $completion) {
    final String _sql = "SELECT * FROM local_scan_items WHERE session_id = ? ORDER BY row_number ASC, created_at ASC";
    return DBUtil.performSuspending(__db, true, false, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        if (sessionId == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, sessionId);
        }
        final int _columnIndexOfId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "id");
        final int _columnIndexOfSessionId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "session_id");
        final int _columnIndexOfRowNumber = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "row_number");
        final int _columnIndexOfSerialNumber = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "serial_number");
        final int _columnIndexOfCellPhoneNumber = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "cell_phone_number");
        final int _columnIndexOfImei1 = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "imei1");
        final int _columnIndexOfImei2 = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "imei2");
        final int _columnIndexOfSource = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "source");
        final int _columnIndexOfCreatedAt = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "created_at");
        final int _columnIndexOfSent = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "sent");
        final int _columnIndexOfSentAt = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "sent_at");
        final int _columnIndexOfLastError = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "last_error");
        final List<LocalScanItemEntity> _result = new ArrayList<LocalScanItemEntity>();
        while (_stmt.step()) {
          final LocalScanItemEntity _item;
          final String _tmpId;
          if (_stmt.isNull(_columnIndexOfId)) {
            _tmpId = null;
          } else {
            _tmpId = _stmt.getText(_columnIndexOfId);
          }
          final String _tmpSessionId;
          if (_stmt.isNull(_columnIndexOfSessionId)) {
            _tmpSessionId = null;
          } else {
            _tmpSessionId = _stmt.getText(_columnIndexOfSessionId);
          }
          final int _tmpRowNumber;
          _tmpRowNumber = (int) (_stmt.getLong(_columnIndexOfRowNumber));
          final String _tmpSerialNumber;
          if (_stmt.isNull(_columnIndexOfSerialNumber)) {
            _tmpSerialNumber = null;
          } else {
            _tmpSerialNumber = _stmt.getText(_columnIndexOfSerialNumber);
          }
          final String _tmpCellPhoneNumber;
          if (_stmt.isNull(_columnIndexOfCellPhoneNumber)) {
            _tmpCellPhoneNumber = null;
          } else {
            _tmpCellPhoneNumber = _stmt.getText(_columnIndexOfCellPhoneNumber);
          }
          final String _tmpImei1;
          if (_stmt.isNull(_columnIndexOfImei1)) {
            _tmpImei1 = null;
          } else {
            _tmpImei1 = _stmt.getText(_columnIndexOfImei1);
          }
          final String _tmpImei2;
          if (_stmt.isNull(_columnIndexOfImei2)) {
            _tmpImei2 = null;
          } else {
            _tmpImei2 = _stmt.getText(_columnIndexOfImei2);
          }
          final String _tmpSource;
          if (_stmt.isNull(_columnIndexOfSource)) {
            _tmpSource = null;
          } else {
            _tmpSource = _stmt.getText(_columnIndexOfSource);
          }
          final long _tmpCreatedAt;
          _tmpCreatedAt = _stmt.getLong(_columnIndexOfCreatedAt);
          final boolean _tmpSent;
          final int _tmp;
          _tmp = (int) (_stmt.getLong(_columnIndexOfSent));
          _tmpSent = _tmp != 0;
          final Long _tmpSentAt;
          if (_stmt.isNull(_columnIndexOfSentAt)) {
            _tmpSentAt = null;
          } else {
            _tmpSentAt = _stmt.getLong(_columnIndexOfSentAt);
          }
          final String _tmpLastError;
          if (_stmt.isNull(_columnIndexOfLastError)) {
            _tmpLastError = null;
          } else {
            _tmpLastError = _stmt.getText(_columnIndexOfLastError);
          }
          _item = new LocalScanItemEntity(_tmpId,_tmpSessionId,_tmpRowNumber,_tmpSerialNumber,_tmpCellPhoneNumber,_tmpImei1,_tmpImei2,_tmpSource,_tmpCreatedAt,_tmpSent,_tmpSentAt,_tmpLastError);
          _result.add(_item);
        }
        return _result;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Flow<List<LocalScanItemEntity>> observeItems(final String sessionId) {
    final String _sql = "SELECT * FROM local_scan_items WHERE session_id = ? ORDER BY row_number ASC, created_at ASC";
    return FlowUtil.createFlow(__db, false, new String[] {"local_scan_items"}, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        if (sessionId == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, sessionId);
        }
        final int _columnIndexOfId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "id");
        final int _columnIndexOfSessionId = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "session_id");
        final int _columnIndexOfRowNumber = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "row_number");
        final int _columnIndexOfSerialNumber = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "serial_number");
        final int _columnIndexOfCellPhoneNumber = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "cell_phone_number");
        final int _columnIndexOfImei1 = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "imei1");
        final int _columnIndexOfImei2 = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "imei2");
        final int _columnIndexOfSource = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "source");
        final int _columnIndexOfCreatedAt = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "created_at");
        final int _columnIndexOfSent = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "sent");
        final int _columnIndexOfSentAt = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "sent_at");
        final int _columnIndexOfLastError = SQLiteStatementUtil.getColumnIndexOrThrow(_stmt, "last_error");
        final List<LocalScanItemEntity> _result = new ArrayList<LocalScanItemEntity>();
        while (_stmt.step()) {
          final LocalScanItemEntity _item;
          final String _tmpId;
          if (_stmt.isNull(_columnIndexOfId)) {
            _tmpId = null;
          } else {
            _tmpId = _stmt.getText(_columnIndexOfId);
          }
          final String _tmpSessionId;
          if (_stmt.isNull(_columnIndexOfSessionId)) {
            _tmpSessionId = null;
          } else {
            _tmpSessionId = _stmt.getText(_columnIndexOfSessionId);
          }
          final int _tmpRowNumber;
          _tmpRowNumber = (int) (_stmt.getLong(_columnIndexOfRowNumber));
          final String _tmpSerialNumber;
          if (_stmt.isNull(_columnIndexOfSerialNumber)) {
            _tmpSerialNumber = null;
          } else {
            _tmpSerialNumber = _stmt.getText(_columnIndexOfSerialNumber);
          }
          final String _tmpCellPhoneNumber;
          if (_stmt.isNull(_columnIndexOfCellPhoneNumber)) {
            _tmpCellPhoneNumber = null;
          } else {
            _tmpCellPhoneNumber = _stmt.getText(_columnIndexOfCellPhoneNumber);
          }
          final String _tmpImei1;
          if (_stmt.isNull(_columnIndexOfImei1)) {
            _tmpImei1 = null;
          } else {
            _tmpImei1 = _stmt.getText(_columnIndexOfImei1);
          }
          final String _tmpImei2;
          if (_stmt.isNull(_columnIndexOfImei2)) {
            _tmpImei2 = null;
          } else {
            _tmpImei2 = _stmt.getText(_columnIndexOfImei2);
          }
          final String _tmpSource;
          if (_stmt.isNull(_columnIndexOfSource)) {
            _tmpSource = null;
          } else {
            _tmpSource = _stmt.getText(_columnIndexOfSource);
          }
          final long _tmpCreatedAt;
          _tmpCreatedAt = _stmt.getLong(_columnIndexOfCreatedAt);
          final boolean _tmpSent;
          final int _tmp;
          _tmp = (int) (_stmt.getLong(_columnIndexOfSent));
          _tmpSent = _tmp != 0;
          final Long _tmpSentAt;
          if (_stmt.isNull(_columnIndexOfSentAt)) {
            _tmpSentAt = null;
          } else {
            _tmpSentAt = _stmt.getLong(_columnIndexOfSentAt);
          }
          final String _tmpLastError;
          if (_stmt.isNull(_columnIndexOfLastError)) {
            _tmpLastError = null;
          } else {
            _tmpLastError = _stmt.getText(_columnIndexOfLastError);
          }
          _item = new LocalScanItemEntity(_tmpId,_tmpSessionId,_tmpRowNumber,_tmpSerialNumber,_tmpCellPhoneNumber,_tmpImei1,_tmpImei2,_tmpSource,_tmpCreatedAt,_tmpSent,_tmpSentAt,_tmpLastError);
          _result.add(_item);
        }
        return _result;
      } finally {
        _stmt.close();
      }
    });
  }

  @Override
  public Object deleteItems(final String sessionId, final Continuation<? super Unit> $completion) {
    final String _sql = "DELETE FROM local_scan_items WHERE session_id = ?";
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        if (sessionId == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, sessionId);
        }
        _stmt.step();
        return Unit.INSTANCE;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Object deleteSession(final String sessionId,
      final Continuation<? super Unit> $completion) {
    final String _sql = "DELETE FROM local_scan_sessions WHERE id = ?";
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        if (sessionId == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, sessionId);
        }
        _stmt.step();
        return Unit.INSTANCE;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Object markItemSent(final String itemId, final long sentAt,
      final Continuation<? super Unit> $completion) {
    final String _sql = "UPDATE local_scan_items SET sent = 1, sent_at = ?, last_error = NULL WHERE id = ?";
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        _stmt.bindLong(_argIndex, sentAt);
        _argIndex = 2;
        if (itemId == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, itemId);
        }
        _stmt.step();
        return Unit.INSTANCE;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Object markItemFailed(final String itemId, final String error,
      final Continuation<? super Unit> $completion) {
    final String _sql = "UPDATE local_scan_items SET last_error = ? WHERE id = ?";
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
        if (itemId == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, itemId);
        }
        _stmt.step();
        return Unit.INSTANCE;
      } finally {
        _stmt.close();
      }
    }, $completion);
  }

  @Override
  public Object updateSessionSendStatus(final String sessionId, final LocalScanSessionStatus status,
      final Long sentAt, final long updatedAt, final Continuation<? super Unit> $completion) {
    final String _sql = "UPDATE local_scan_sessions SET status = ?, sent_at = ?, updated_at = ? WHERE id = ?";
    return DBUtil.performSuspending(__db, false, true, (_connection) -> {
      final SQLiteStatement _stmt = _connection.prepare(_sql);
      try {
        int _argIndex = 1;
        final String _tmp = Converters.fromLocalScanSessionStatus(status);
        if (_tmp == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, _tmp);
        }
        _argIndex = 2;
        if (sentAt == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindLong(_argIndex, sentAt);
        }
        _argIndex = 3;
        _stmt.bindLong(_argIndex, updatedAt);
        _argIndex = 4;
        if (sessionId == null) {
          _stmt.bindNull(_argIndex);
        } else {
          _stmt.bindText(_argIndex, sessionId);
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
