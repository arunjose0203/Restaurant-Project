import { useEffect, useState } from 'react';
import { View, Text, TextInput } from 'react-native';
import type { User, State } from '../../web/src/types';
import { Button, s } from '../ui';

export function StaffTools({
  user,
  data,
  api,
  onSession,
  refresh,
}: {
  user: User;
  data: State;
  api: (p: string, m?: string, b?: unknown) => Promise<any>;
  onSession: (session: any) => Promise<void>;
  refresh: () => Promise<void>;
}) {
  const [open, setOpen] = useState(false);
  const [branches, setBranches] = useState<any[]>([]);
  const [staff, setStaff] = useState<any[]>([]);
  const [pin, setPin] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [source, setSource] = useState('');
  const [target, setTarget] = useState(0);

  useEffect(() => {
    if (open) {
      Promise.all([api('/auth/branches'), api('/auth/staff')])
        .then(([b, s]) => {
          setBranches(b);
          setStaff(s);
        })
        .catch(e => setError(e.message));
    }
  }, [open]);

  async function run(work: () => Promise<unknown>) {
    setBusy(true);
    setError('');
    try {
      await work();
      await refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <View>
      <Button
        title={open ? 'Close staff tools' : 'Staff, branch & floor tools'}
        secondary
        onPress={() => setOpen(!open)}
      />
      {open && (
        <View style={s.card}>
          {!!error && <Text style={s.error}>{error}</Text>}
          <Text style={s.h2}>Branch</Text>
          {branches.map(b => (
            <Button
              key={b.id}
              title={b.name}
              secondary={b.id !== user.branchId}
              disabled={busy || b.id === user.branchId}
              onPress={() =>
                void run(async () => {
                  const res = await api('/auth/branch/' + b.id, 'POST', {});
                  await onSession(res);
                })
              }
            />
          ))}

          <Text style={s.label}>Four-digit PIN</Text>
          <TextInput
            style={s.input}
            secureTextEntry
            keyboardType="number-pad"
            maxLength={4}
            value={pin}
            onChangeText={v => setPin(v.replace(/\D/g, ''))}
          />
          <Button
            title="Set my PIN"
            disabled={busy || pin.length !== 4}
            onPress={() => void run(() => api('/auth/pin', 'POST', { pin }))}
          />

          <Text style={s.h2}>Unlock staff</Text>
          {staff.map(person => (
            <Button
              key={person.id}
              title={person.name}
              secondary
              disabled={busy || pin.length !== 4}
              onPress={() =>
                void run(async () => {
                  const res = await api('/auth/pin-login', 'POST', {
                    userId: person.id,
                    pin,
                    branchId: user.branchId || 1,
                  });
                  await onSession(res);
                })
              }
            />
          ))}

          {['Admin', 'Cashier'].includes(user.role) && (
            <>
              <Text style={s.h2}>Transfer / merge visit</Text>
              {data.sessions.map(session => (
                <Button
                  key={session.id}
                  title={`From table ${session.tableId}`}
                  secondary={source !== session.id}
                  onPress={() => setSource(session.id)}
                />
              ))}
              <Text style={s.label}>Target table</Text>
              <View style={s.wrap}>
                {data.tables.map(t => (
                  <Button
                    key={t.id}
                    title={t.name}
                    secondary={target !== t.id}
                    onPress={() => setTarget(t.id)}
                  />
                ))}
              </View>
              {[false, true].map(merge => (
                <Button
                  key={String(merge)}
                  title={merge ? 'Merge visits' : 'Transfer visit'}
                  disabled={busy || !source || !target}
                  onPress={() =>
                    void run(() =>
                      api('/pos/sessions/' + source + '/transfer', 'POST', {
                        tableId: target,
                        merge,
                      })
                    )
                  }
                />
              ))}
            </>
          )}

          <Text style={s.h2}>Guest requests</Text>
          {((data as any).guestRequests || []).map((r: any) => (
            <Button
              key={r.id}
              title={`Resolve ${r.kind} · Table ${r.tableId}`}
              secondary
              disabled={busy}
              onPress={() =>
                void run(() =>
                  api('/pos/guest-requests/' + r.id + '/resolve', 'POST', {})
                )
              }
            />
          ))}
        </View>
      )}
    </View>
  );
}
