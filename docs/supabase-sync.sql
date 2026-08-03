-- Run once in the Supabase SQL editor. The app uses the anon key; share codes are
-- capabilities and only their SHA-256 hashes are stored.
create extension if not exists pgcrypto;

create table if not exists public.recipe_shopper_workspaces (
  id uuid primary key default gen_random_uuid(),
  code_hash bytea not null unique,
  snapshot jsonb not null,
  revision bigint not null default 1,
  updated_at timestamptz not null default now()
);

alter table public.recipe_shopper_workspaces enable row level security;
revoke all on public.recipe_shopper_workspaces from anon, authenticated;

create or replace function public.create_recipe_shopper_workspace(p_snapshot text)
returns jsonb
language plpgsql security definer set search_path = public
as $$
declare
  alphabet constant text := 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  code text := '';
  workspace public.recipe_shopper_workspaces;
  i integer;
begin
  for i in 1..8 loop
    code := code || substr(alphabet, 1 + floor(random() * length(alphabet))::integer, 1);
  end loop;
  insert into public.recipe_shopper_workspaces(code_hash, snapshot)
  values (digest(code, 'sha256'), p_snapshot::jsonb)
  returning * into workspace;
  return jsonb_build_object('workspaceId', workspace.id, 'shareCode',
    substr(code, 1, 4) || '-' || substr(code, 5, 4), 'revision', workspace.revision);
end;
$$;

create or replace function public.join_recipe_shopper_workspace(p_share_code text)
returns jsonb
language sql security definer set search_path = public
as $$
  select jsonb_build_object('workspaceId', id, 'revision', revision, 'snapshot', snapshot::text)
  from public.recipe_shopper_workspaces
  where code_hash = digest(upper(regexp_replace(p_share_code, '[^A-Z0-9]', '', 'g')), 'sha256');
$$;

create or replace function public.pull_recipe_shopper_workspace(
  p_workspace_id uuid, p_share_code text, p_after_revision bigint)
returns jsonb
language sql security definer set search_path = public
as $$
  select jsonb_build_object('workspaceId', id, 'revision', revision, 'snapshot', snapshot::text)
  from public.recipe_shopper_workspaces
  where id = p_workspace_id
    and revision > p_after_revision
    and code_hash = digest(upper(regexp_replace(p_share_code, '[^A-Z0-9]', '', 'g')), 'sha256');
$$;

create or replace function public.push_recipe_shopper_workspace(
  p_workspace_id uuid, p_share_code text, p_expected_revision bigint, p_snapshot text)
returns jsonb
language plpgsql security definer set search_path = public
as $$
declare workspace public.recipe_shopper_workspaces;
begin
  update public.recipe_shopper_workspaces
     set snapshot = p_snapshot::jsonb, revision = revision + 1, updated_at = now()
   where id = p_workspace_id
     and revision = p_expected_revision
     and code_hash = digest(upper(regexp_replace(p_share_code, '[^A-Z0-9]', '', 'g')), 'sha256')
  returning * into workspace;
  if workspace.id is null then
    raise exception 'Workspace changed or code is invalid' using errcode = '40001';
  end if;
  return jsonb_build_object('workspaceId', workspace.id, 'revision', workspace.revision, 'snapshot', workspace.snapshot::text);
end;
$$;

revoke all on function public.create_recipe_shopper_workspace(text) from public;
revoke all on function public.join_recipe_shopper_workspace(text) from public;
revoke all on function public.pull_recipe_shopper_workspace(uuid, text, bigint) from public;
revoke all on function public.push_recipe_shopper_workspace(uuid, text, bigint, text) from public;
grant execute on function public.create_recipe_shopper_workspace(text) to anon, authenticated;
grant execute on function public.join_recipe_shopper_workspace(text) to anon, authenticated;
grant execute on function public.pull_recipe_shopper_workspace(uuid, text, bigint) to anon, authenticated;
grant execute on function public.push_recipe_shopper_workspace(uuid, text, bigint, text) to anon, authenticated;
