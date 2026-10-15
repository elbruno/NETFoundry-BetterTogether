import { episodes, calendar } from '../../lib/episodes.mjs';
import type { APIRoute } from 'astro';

export function getStaticPaths() {
  return episodes.map((episode) => ({ params: { id: episode.id }, props: { episode } }));
}

export const GET: APIRoute = ({ props }) => new Response(calendar(props.episode), {
  headers: { 'Content-Type': 'text/calendar; charset=utf-8' },
});
